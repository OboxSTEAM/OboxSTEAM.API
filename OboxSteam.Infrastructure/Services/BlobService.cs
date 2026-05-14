using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public class BlobService : IBlobService
{
    private readonly string _bucketName = "oboxsteam-bucket";
    private readonly ILogger<BlobService> _logger;
    private readonly IMinioClient _minioClient;

    /// <summary>
    /// IMinioClient is injected via DI (registered as singleton in IocContainer).
    /// This makes BlobService fully testable with a mocked client.
    /// </summary>
    public BlobService(IMinioClient minioClient, ILogger<BlobService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    /// <summary>
    ///     Check xem bucket đã tồn tại trên Minio chưa (creates nếu bucket ko tồn tại).
    /// </summary>
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName), cancellationToken);

            if (!exists)
            {
                _logger.LogWarning("Bucket '{Bucket}' not found. Creating...", _bucketName);
                await _minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName), cancellationToken);
                _logger.LogInformation("Bucket '{Bucket}' created.", _bucketName);
                await SetPublicPolicyAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Bucket '{Bucket}' already exists.", _bucketName);
            }
        }
        catch (MinioException mex)
        {
            _logger.LogError("MinIO error in EnsureBucketExists: {Message}", mex.Message);
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

    private async Task SetPublicPolicyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = $$"""
            {
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Principal": {"AWS": "*"},
                        "Action": ["s3:GetObject"],
                        "Resource": ["arn:aws:s3:::{{_bucketName}}/*"]
                    }
                ]
            }
            """;

            await _minioClient.SetPolicyAsync(new SetPolicyArgs()
                .WithBucket(_bucketName)
                .WithPolicy(policy), cancellationToken);

            _logger.LogInformation("Public read policy set for bucket '{Bucket}'", _bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to set public policy: {Message}", ex.Message);
        }
    }

    /// <summary>Upload file lên MinIO bucket.</summary>
    public async Task UploadFileAsync(string fileName, Stream fileStream, string? folder = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var objectName = string.IsNullOrWhiteSpace(folder)
            ? fileName
            : $"{folder.TrimEnd('/')}/{fileName}";

        var contentType = GetContentType(fileName);

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType);

        _logger.LogInformation("Uploading '{Object}' (type={ContentType})...", objectName, contentType);
        await _minioClient.PutObjectAsync(putArgs, cancellationToken);
        _logger.LogInformation("Upload completed: {Object}", objectName);
    }

    /// <summary>Tạo 1 cái preview URL cho file đã upload lên MinIO.</summary>
    public Task<string> GetPreviewUrlAsync(string fileName)
    {
        var minioHost = Environment.GetEnvironmentVariable("MINIO_HOST") ?? "http://localhost:9001/";
        var previewUrl = $"{minioHost.TrimEnd('/')}/{_bucketName}/{fileName}";
        _logger.LogInformation("Preview URL: {Url}", previewUrl);
        return Task.FromResult(previewUrl);
    }

    /// <summary>Generates a presigned URL for downloading a file from MinIO.</summary>
    public async Task<string> GetFileUrlAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName)
                .WithExpiry(7 * 24 * 60 * 60);

            var presignTask = _minioClient.PresignedGetObjectAsync(args);

            var url = cancellationToken == default
                ? await presignTask
                : await presignTask.WaitAsync(cancellationToken);

            var minioHost = Environment.GetEnvironmentVariable("MINIO_HOST") ?? "http://localhost:9001/";
            url = url.Replace("http://minio:9000", minioHost.TrimEnd('/'))
                     .Replace("http://minio:9001", minioHost.TrimEnd('/'))
                     .Replace("http://localhost:9000", minioHost.TrimEnd('/'))
                     .Replace("http://localhost:9001", minioHost.TrimEnd('/'));

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

    /// <summary>Delete a file from MinIO storage.</summary>
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

            var objectName = path.StartsWith($"/{_bucketName}/")
                ? path.Substring($"/{_bucketName}/".Length)
                : path.TrimStart('/');

            _logger.LogInformation("Deleting file from MinIO: {Object}", objectName);

            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
            _logger.LogInformation("File deleted successfully: {Object}", objectName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("File deletion cancelled: {Url}", fileUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from MinIO: {Url}", fileUrl);
            // Don't throw — old file deletion failure shouldn't block a new upload
        }
    }

    private string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".pdf"            => "application/pdf",
            ".mp4"            => "video/mp4",
            _                 => "application/octet-stream"
        };
    }
}
