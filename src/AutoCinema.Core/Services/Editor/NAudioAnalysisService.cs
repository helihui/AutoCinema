using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace AutoCinema.Pro.Services.Editor;

/// <summary>
/// 使用 NAudio 实现的音频分析服务
/// </summary>
public class NAudioAnalysisService : IAudioAnalysisService
{
    private readonly ILogger<NAudioAnalysisService> _logger;

    public NAudioAnalysisService(ILogger<NAudioAnalysisService> logger)
    {
        _logger = logger;
    }

    public Task<TimeSpan> GetDurationAsync(string audioPath, CancellationToken ct = default)
    {
        if (!File.Exists(audioPath))
        {
            throw new FileNotFoundException($"音频文件不存在: {audioPath}");
        }

        try
        {
            using var reader = new Mp3FileReader(audioPath);
            var duration = reader.TotalTime;

            _logger.LogDebug("音频时长: {Duration:mm\\:ss\\.fff} - {Path}", duration, Path.GetFileName(audioPath));

            return Task.FromResult(duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取音频时长失败: {Path}", audioPath);
            throw;
        }
    }
}
