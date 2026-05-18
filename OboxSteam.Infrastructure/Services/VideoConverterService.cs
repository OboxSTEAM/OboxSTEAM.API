using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Transcodes video to H.264/AAC MP4 using FFmpeg running locally inside the Docker container.
/// Workflow: ffmpeg reads from a local /tmp file → transcodes → uploads result to S3 → cleanup.
/// No S3 download needed — the caller is responsible for providing a local input path.
/// </summary>
public class VideoConverterService : IVideoConverterService
{
    private const string EnvS3Bucket = "AWS_S3_BUCKET";

    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<VideoConverterService> _logger;

    public VideoConverterService(
        IAmazonS3 s3Client,
        ILogger<VideoConverterService> logger)
    {
        _s3Client = s3Client;
        _logger   = logger;
    }

    /// <inheritdoc />
    public async Task<string> ConvertToH264Async(string inputLocalPath, string outputS3Key)
    {
        var bucket = RequireEnv(EnvS3Bucket);

        if (!File.Exists(inputLocalPath))
            throw new FileNotFoundException($"Input file not found: {inputLocalPath}");

        // Output goes into the same temp directory as the input
        var tmpDir     = Path.GetDirectoryName(inputLocalPath)!;
        var outputPath = Path.Combine(tmpDir, "out_" + Path.GetFileName(outputS3Key));

        try
        {
            // ── 1. Run FFmpeg ────────────────────────────────────────────────
            _logger.LogInformation("Running FFmpeg: {Input} → {Output}", inputLocalPath, outputPath);
            await RunFfmpegAsync(inputLocalPath, outputPath);
            _logger.LogInformation("FFmpeg transcoding complete: {Output}", outputPath);

            // ── 2. Upload transcoded file to S3 ──────────────────────────────
            _logger.LogInformation("Uploading to S3: s3://{Bucket}/{Key}", bucket, outputS3Key);
            await UploadToS3Async(bucket, outputS3Key, outputPath);
            _logger.LogInformation("Upload complete: s3://{Bucket}/{Key}", bucket, outputS3Key);

            return outputS3Key;
        }
        finally
        {
            // ── Cleanup output temp file (input temp dir cleaned by caller) ──
            try { if (File.Exists(outputPath)) File.Delete(outputPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to cleanup output temp file: {Path}", outputPath); }
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private async Task UploadToS3Async(string bucket, string key, string sourcePath)
    {
        var request = new PutObjectRequest
        {
            BucketName  = bucket,
            Key         = key,
            FilePath    = sourcePath,
            ContentType = "video/mp4"
        };
        await _s3Client.PutObjectAsync(request);
    }

    /// <summary>
    /// Invokes ffmpeg to transcode <paramref name="inputPath"/> to H.264/AAC MP4 at
    /// <paramref name="outputPath"/>. Throws <see cref="InvalidOperationException"/> on non-zero exit.
    /// </summary>
    private async Task RunFfmpegAsync(string inputPath, string outputPath)
    {
        // -y          : overwrite output without asking
        // -i          : input file
        // -c:v libx264: H.264 video codec
        // -crf 23     : quality (18=lossless, 28=fast; 23 is a good default)
        // -preset fast: encoding speed/compression tradeoff
        // -c:a aac    : AAC audio codec
        // -b:a 96k    : 96 kbps audio bitrate
        // -movflags +faststart : move MP4 moov atom to front for streaming
        var args = $"-y -i \"{inputPath}\" " +
                   $"-c:v libx264 -crf 23 -preset fast " +
                   $"-c:a aac -b:a 96k " +
                   $"-movflags +faststart " +
                   $"\"{outputPath}\"";

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "ffmpeg",
            Arguments              = args,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        // Capture stderr (ffmpeg logs to stderr)
        var stderrLines = new System.Collections.Generic.List<string>();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderrLines.Add(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var stderr = string.Join("\n", stderrLines);
            _logger.LogError("FFmpeg failed (exit {Code}):\n{Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException(
                $"FFmpeg exited with code {process.ExitCode}. See logs for details.");
        }

        _logger.LogDebug("FFmpeg stderr:\n{Stderr}", string.Join("\n", stderrLines));
    }

    private static string RequireEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Required environment variable '{key}' is not set.");
}
