namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Transcodes a video file to H.264/AAC MP4 using local FFmpeg,
/// then uploads the result directly to S3.
/// </summary>
public interface IVideoConverterService
{
    /// <summary>
    /// Transcodes the file at <paramref name="inputLocalPath"/> via FFmpeg and
    /// uploads the result to <paramref name="outputS3Key"/> in S3.
    /// Cleans up all temp files on completion or failure.
    /// </summary>
    /// <param name="inputLocalPath">Absolute local path of the source video (e.g. "/tmp/upload_xxx/video.mp4").</param>
    /// <param name="outputS3Key">Desired S3 object key for the transcoded output (e.g. "media/video.mp4").</param>
    /// <returns>The final S3 key of the transcoded file.</returns>
    Task<string> ConvertToH264Async(string inputLocalPath, string outputS3Key);
}
