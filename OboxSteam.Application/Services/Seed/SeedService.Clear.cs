using Microsoft.Extensions.Logging;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task ClearS3ObjectsAsync()
    {
        var excludedPrefix = $"{SeedS3Folder}/";
        _loggerService.LogInformation(
            "[ClearS3] Clearing all S3 objects except '{Folder}' folder (prefix '{Prefix}')...",
            SeedS3Folder,
            excludedPrefix);

        var (deleted, failed) = await _blobService.ClearAllObjectsExceptPrefixAsync(excludedPrefix);

        _loggerService.LogInformation(
            "[ClearS3] S3 cleanup done. Deleted={Deleted}, Failed={Failed}",
            deleted,
            failed);
    }
}
