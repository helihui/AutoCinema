using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Services.Editor;

/// <summary>
/// FFMpeg 视频合成服务
/// </summary>
public class FFMpegVideoService : IVideoCompositionService
{
    private readonly ILogger<FFMpegVideoService> _logger;
    private readonly PipelineOptions _options;

    public FFMpegVideoService(
        ILogger<FFMpegVideoService> logger,
        IOptions<PipelineOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> ComposeAsync(
        IEnumerable<GeneratedAsset> assets,
        string subtitlePath,
        string outputPath,
        CancellationToken ct = default)
    {
        var orderedAssets = assets.OrderBy(a => a.SceneIndex).ToList();
        var tempDir = Path.Combine(_options.TempDirectory, Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            _logger.LogInformation("开始视频合成，共 {Count} 个场景", orderedAssets.Count);

            var segmentPaths = new List<string>();

            // 1. 为每个场景生成独立的视频片段
            for (int i = 0; i < orderedAssets.Count; i++)
            {
                var asset = orderedAssets[i];
                var segmentPath = Path.Combine(tempDir, $"segment_{i:D3}.ts");
                segmentPaths.Add(segmentPath);

                _logger.LogDebug("生成片段 {Index}/{Total}: {Duration:mm\\:ss\\.fff}",
                    i + 1, orderedAssets.Count, asset.AudioDuration);

                // 使用 -loop 1 -t [duration] 将静态图片转为视频
                // 这是音画对齐的拉伸策略
                // 添加 scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2
                // 确保输出为标准 1080p，避免高分辨率导致的内存问题
                await FFMpegArguments
                    .FromFileInput(asset.ImagePath, verifyExists: true, options => options
                        .Loop(1)
                        .WithDuration(asset.AudioDuration))
                    .AddFileInput(asset.AudioPath, verifyExists: true)
                    .OutputToFile(segmentPath, overwrite: true, options => options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithFramerate(_options.FrameRate)
                        .WithConstantRateFactor(_options.VideoQuality)
                        .ForceFormat("mpegts")
                        .WithCustomArgument("-vf \"scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2\"")
                        .WithCustomArgument("-shortest"))
                    .ProcessAsynchronously();

                _logger.LogInformation("片段生成完成: segment_{Index:D3}.ts", i);
            }

            // 2. 生成 concat 列表文件
            var concatListPath = Path.Combine(tempDir, "concat_list.txt");
            // 使用绝对路径，并转换为 Unix 风格的路径分隔符
            var concatContent = string.Join(Environment.NewLine,
                segmentPaths.Select(p =>
                {
                    var absolutePath = Path.GetFullPath(p);
                    var unixPath = absolutePath.Replace("\\", "/");
                    return $"file '{unixPath.Replace("'", @"'\''")}'";
                }));
            await File.WriteAllTextAsync(concatListPath, concatContent, ct);

            _logger.LogDebug("Concat 列表文件: {Path}", concatListPath);

            // 3. 合并所有片段
            var tempMergedPath = Path.Combine(tempDir, "merged.mp4");
            await FFMpegArguments
                .FromFileInput(concatListPath, verifyExists: true, options => options
                    .ForceFormat("concat")
                    .WithCustomArgument("-safe 0"))
                .OutputToFile(tempMergedPath, overwrite: true, options => options
                    .CopyChannel()
                    .WithFastStart())
                .ProcessAsynchronously();

            _logger.LogInformation("片段合并完成: {Path}", tempMergedPath);

            // 4. 烧录字幕 (Hardcoding)
            // 确保输出目录存在
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 转义字幕路径中的特殊字符
            var escapedSubtitlePath = subtitlePath
                .Replace("\\", "/")
                .Replace(":", "\\:")
                .Replace("'", @"'\''");

            await FFMpegArguments
                .FromFileInput(tempMergedPath, verifyExists: true)
                .OutputToFile(outputPath, overwrite: true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithConstantRateFactor(_options.VideoQuality)
                    .WithFastStart()
                    .WithCustomArgument($"-vf \"subtitles='{escapedSubtitlePath}':force_style='FontSize=24,PrimaryColour=&HFFFFFF,OutlineColour=&H000000,Outline=2'\""))
                .ProcessAsynchronously();

            _logger.LogInformation("视频合成完成: {Path}", outputPath);

            return outputPath;
        }
        finally
        {
            // 清理临时文件
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                    _logger.LogDebug("临时文件已清理: {Path}", tempDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理临时文件失败: {Path}", tempDir);
            }
        }
    }
}
