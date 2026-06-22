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

    /// <summary>
    /// Xóa toàn bộ objects trong bucket hiện tại.
    /// Trả về số object đã xóa và số object xóa thất bại.
    /// </summary>
    Task<(int Deleted, int Failed)> ClearAllObjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa objects có key bắt đầu bằng <paramref name="prefix"/> (e.g. "Seed/").
    /// Trả về số object đã xóa và số object xóa thất bại.
    /// </summary>
    Task<(int Deleted, int Failed)> ClearObjectsByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa toàn bộ objects trong bucket, trừ những object có key bắt đầu bằng
    /// <paramref name="excludedPrefix"/> (e.g. "Seed/" để giữ seed assets).
    /// </summary>
    Task<(int Deleted, int Failed)> ClearAllObjectsExceptPrefixAsync(
        string excludedPrefix,
        CancellationToken cancellationToken = default);
}