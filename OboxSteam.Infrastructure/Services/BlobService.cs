using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public class BlobService : IBlobService
{
    private readonly string _bucketName;
    private readonly string _region;
    private readonly ILogger<BlobService> _logger;
    private readonly IAmazonS3 _s3Client;
    private readonly Amazon.S3.Transfer.TransferUtility _transferUtility;
    private static bool _bucketInitialized = false;

    /// <summary>
    /// IAmazonS3 is injected via DI (registered as singleton in IocContainer).
    /// This makes BlobService fully testable with a mocked client.
    /// </summary>
    public BlobService(IAmazonS3 s3Client, ILogger<BlobService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = Environment.GetEnvironmentVariable("AWS_S3_BUCKET") ?? "oboxsteam-bucket";
        _region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
        _transferUtility = new Amazon.S3.Transfer.TransferUtility(_s3Client);
    }

    /// <inheritdoc />
    public string BucketName => _bucketName;


    /// <summary>
    ///     Check xem bucket đã tồn tại trên S3 chưa (creates nếu bucket ko tồn tại).
    /// </summary>
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_bucketInitialized)
            return;

        try
        {
            var buckets = await _s3Client.ListBucketsAsync(cancellationToken);
            var exists = buckets.Buckets.Exists(b => b.BucketName == _bucketName);

            if (!exists)
            {
                _logger.LogWarning("Bucket '{Bucket}' not found. Creating...", _bucketName);
                await _s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _bucketName,
                    UseClientRegion = true
                }, cancellationToken);
                _logger.LogInformation("Bucket '{Bucket}' created.", _bucketName);
            }
            else
            {
                _logger.LogInformation("Bucket '{Bucket}' already exists.", _bucketName);
            }

            // Always ensure public-read policy is applied
            await SetPublicReadPolicyAsync(cancellationToken);
            _bucketInitialized = true;
        }
        catch (AmazonS3Exception s3Ex)
        {
            _logger.LogError("S3 error in EnsureBucketExists: {Message}", s3Ex.Message);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Bucket creation cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error in EnsureBucketExists: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Sets a public-read policy on the bucket so that objects can be viewed/downloaded
    /// via direct URL (e.g. https://{bucket}.s3.{region}.amazonaws.com/{key}).
    /// Also disables S3 Block Public Access for the bucket.
    /// </summary>
    private async Task SetPublicReadPolicyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Disable Block Public Access so the bucket policy takes effect
            await _s3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
            {
                BucketName = _bucketName,
                PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                {
                    BlockPublicAcls = false,
                    IgnorePublicAcls = false,
                    BlockPublicPolicy = false,
                    RestrictPublicBuckets = false
                }
            }, cancellationToken);

            var policy = $$"""
            {
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Sid": "PublicReadGetObject",
                        "Effect": "Allow",
                        "Principal": "*",
                        "Action": ["s3:GetObject"],
                        "Resource": ["arn:aws:s3:::{{_bucketName}}/*"]
                    }
                ]
            }
            """;

            await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = _bucketName,
                Policy = policy
            }, cancellationToken);

            _logger.LogInformation("Public read policy set for bucket '{Bucket}'", _bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to set public read policy: {Message}", ex.Message);
        }
    }

    /// <summary>Upload file lên S3 bucket.</summary>
    public async Task UploadFileAsync(string fileName, Stream fileStream, string? folder = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var objectKey = string.IsNullOrWhiteSpace(folder)
            ? fileName
            : $"{folder.TrimEnd('/')}/{fileName}";

        var contentType = GetContentType(fileName);

        _logger.LogInformation("Uploading '{Object}' (type={ContentType})...", objectKey, contentType);

        var uploadRequest = new Amazon.S3.Transfer.TransferUtilityUploadRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = fileStream,
            ContentType = contentType,
            PartSize = 6 * 1024 * 1024 // upload từng chunk 6MB
        };

        await _transferUtility.UploadAsync(uploadRequest, cancellationToken);
        _logger.LogInformation("Upload completed: {Object}", objectKey);
    }

    /// <summary>Tạo 1 cái preview URL cho file đã upload lên S3.</summary>
    public Task<string> GetPreviewUrlAsync(string fileName)
    {
        var previewUrl = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{fileName}";
        _logger.LogInformation("Preview URL: {Url}", previewUrl);
        return Task.FromResult(previewUrl);
    }

    /// <summary>Generates a presigned URL for downloading a file from S3.</summary>
    public async Task<string> GetFileUrlAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                Expires = DateTime.UtcNow.AddDays(7)
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);

            _logger.LogInformation("Presigned URL generated for: {File}", fileName);
            return url;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Presigned URL generation cancelled: {File}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error generating presigned URL for {File}: {Message}", fileName, ex.Message);
            return string.Empty;
        }
    }

    /// <summary>Delete a file from S3 storage.</summary>
    public async Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.LogWarning("DeleteFileAsync called with null or empty fileUrl.");
            return;
        }

        try
        {
            var uri = new Uri(fileUrl);
            var path = uri.AbsolutePath;

            var objectKey = path.StartsWith($"/{_bucketName}/")
                ? path.Substring($"/{_bucketName}/".Length)
                : path.TrimStart('/');

            _logger.LogInformation("Deleting file from S3: {Object}", objectKey);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("File deleted successfully: {Object}", objectKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("File deletion cancelled: {Url}", fileUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from S3: {Url}", fileUrl);
            // Don't throw — old file deletion failure shouldn't block a new upload
        }
    }

    /// <summary>Deletes an S3 object directly by its bucket-relative key.</summary>
    public async Task DeleteByKeyAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(s3Key))
        {
            _logger.LogWarning("DeleteByKeyAsync called with null or empty key.");
            return;
        }

        try
        {
            _logger.LogInformation("Deleting S3 object by key: {Key}", s3Key);
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            }, cancellationToken);
            _logger.LogInformation("S3 object deleted: {Key}", s3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting S3 object by key: {Key}", s3Key);
            // Non-fatal — same policy as DeleteFileAsync
        }
    }

    private string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }
}
