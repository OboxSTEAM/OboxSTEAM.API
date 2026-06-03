namespace OboxSteam.Application.Interfaces;

public interface IBlobService
{
    /// <summary>
    /// Tên S3 bucket đang được sử dụng (đọc từ env var AWS_S3_BUCKET).
    /// Dùng property này thay vì hardcode bucket name trong các service khác.
    /// </summary>
    string BucketName { get; }

    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        string fileName,
        Stream fileStream,
        string folder,
        CancellationToken cancellationToken = default);

    Task<string> GetPreviewUrlAsync(string fileName);

    Task<string> GetFileUrlAsync(string fileName, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an S3 object by its bucket-relative key (e.g. "raw/video.mov").
    /// Prefer this over <see cref="DeleteFileAsync"/> when you already have the key.
    /// </summary>
    Task DeleteByKeyAsync(string s3Key, CancellationToken cancellationToken = default);
}