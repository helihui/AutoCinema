using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AutoCinema.Pro.Models.Audio;
using FFMpegCore;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Services.Audio;

/// <summary>
/// 基于 FFMpeg 的音频预处理服务实现
/// 功能链：语音段检测 → 提取最佳单人声片段 → 降噪 → 单声道归一化 → 裁切时长 → 格式转换
/// </summary>
public class FFMpegAudioPreprocessorService : IAudioPreprocessorService
{
    private readonly ILogger<FFMpegAudioPreprocessorService> _logger;

    public FFMpegAudioPreprocessorService(ILogger<FFMpegAudioPreprocessorService> logger)
    {
        _logger = logger;
    }

    public async Task<AudioProcessResult> PrepareVoiceCloneAudioAsync(
        string sourceFilePath,
        VoiceCloneRequirements requirements,
        CancellationToken ct = default)
    {
        _logger.LogInformation("开始音频预处理: {SourceFile}", sourceFilePath);

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("源音频文件不存在", sourceFilePath);

        // 第一步：分析原始音频
        var mediaInfo = await FFProbe.AnalyseAsync(sourceFilePath, cancellationToken: ct);
        var originalDuration = mediaInfo.Duration;
        _logger.LogInformation("原始音频时长: {Duration}, 采样率: {SampleRate}Hz",
            originalDuration, mediaInfo.PrimaryAudioStream?.SampleRateHz);

        // 第二步：检测语音段，找出最佳单人声片段
        var bestSegment = await FindBestSpeechSegmentAsync(sourceFilePath, requirements, ct);

        // 第三步：提取最佳片段并应用降噪 + 归一化
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"voice_preprocessed_{Guid.NewGuid():N}.{requirements.TargetFormat}");

        var filters = BuildFilterChain(requirements);
        _logger.LogInformation("提取片段: {Start}s ~ {End}s (时长={Dur}s), 滤波器: {Filters}",
            bestSegment.Start.TotalSeconds, bestSegment.End.TotalSeconds,
            bestSegment.Duration.TotalSeconds, filters);

        var args = FFMpegArguments
            .FromFileInput(sourceFilePath, verifyExists: true, options =>
            {
                // 从最佳片段的起始位置开始
                options.Seek(bestSegment.Start);
                options.WithDuration(bestSegment.Duration);
            })
            .OutputToFile(outputPath, overwrite: true, options =>
            {
                if (!string.IsNullOrEmpty(filters))
                {
                    options.WithCustomArgument($"-af \"{filters}\"");
                }

                options.WithCustomArgument("-ac 1");
                options.WithCustomArgument($"-ar {requirements.TargetSampleRate}");

                if (requirements.TargetFormat == "mp3")
                {
                    options.WithAudioBitrate(128);
                }
            });

        var success = await args.ProcessAsynchronously(throwOnError: false);
        if (!success || !File.Exists(outputPath))
        {
            throw new InvalidOperationException("FFmpeg 音频预处理失败");
        }

        // 第四步：验证处理结果
        var processedInfo = await FFProbe.AnalyseAsync(outputPath, cancellationToken: ct);
        var processedDuration = processedInfo.Duration;
        var fileSize = new FileInfo(outputPath).Length;

        _logger.LogInformation("预处理完成: 时长={Duration}s, 大小={Size}KB",
            processedDuration.TotalSeconds, fileSize / 1024);

        if (processedDuration < requirements.MinDuration)
        {
            TryDeleteFile(outputPath);
            throw new InvalidOperationException(
                $"预处理后的音频时长 ({processedDuration.TotalSeconds:F1}秒) 不满足平台最低要求 ({requirements.MinDuration.TotalSeconds:F0}秒)。" +
                "音频中未检测到足够长的清晰单人语音片段，请提供一段包含至少10秒连续单人说话的音频。");
        }

        if (fileSize > requirements.MaxFileSizeBytes)
        {
            TryDeleteFile(outputPath);
            throw new InvalidOperationException(
                $"预处理后的文件大小 ({fileSize / 1024 / 1024:F1}MB) 超过平台限制 ({requirements.MaxFileSizeBytes / 1024 / 1024}MB)。" +
                "请提供更短的音频文件。");
        }

        return new AudioProcessResult
        {
            ProcessedFilePath = outputPath,
            ActualDuration = processedDuration,
            FileSizeBytes = fileSize
        };
    }

    /// <summary>
    /// 使用 FFmpeg silencedetect 找出所有语音段，返回最长的连续语音段（最可能是单人说话的片段）
    /// </summary>
    private async Task<SpeechSegment> FindBestSpeechSegmentAsync(
        string filePath,
        VoiceCloneRequirements requirements,
        CancellationToken ct)
    {
        _logger.LogInformation("正在检测语音段...");

        // 使用 silencedetect 检测静音区间，反推出有声区间
        // silence_threshold=-30dB: 低于此阈值视为静音
        // silence_duration=0.8: 持续超过 0.8 秒的静音视为分段点
        var ffmpegBinary = GlobalFFOptions.Current.BinaryFolder;
        var ffmpegPath = string.IsNullOrEmpty(ffmpegBinary) ? "ffmpeg" : Path.Combine(ffmpegBinary, "ffmpeg");

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-i \"{filePath}\" -af silencedetect=noise=-30dB:d=0.8 -f null -",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var stderr = new List<string>();
        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderr.Add(e.Data);
        };
        process.Start();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        // 解析 silencedetect 输出
        var silenceIntervals = ParseSilenceDetectOutput(stderr);
        _logger.LogInformation("检测到 {Count} 段静音区间", silenceIntervals.Count);

        // 获取总时长
        var mediaInfo = await FFProbe.AnalyseAsync(filePath, cancellationToken: ct);
        var totalDuration = mediaInfo.Duration.TotalSeconds;

        // 从静音区间反推出有声区间
        var speechSegments = ExtractSpeechSegments(silenceIntervals, totalDuration);
        _logger.LogInformation("提取出 {Count} 段语音区间", speechSegments.Count);

        if (speechSegments.Count == 0)
        {
            throw new InvalidOperationException(
                "未在音频中检测到有效的人声片段。音频可能全是静音或噪音，请提供包含清晰人声的音频文件。");
        }

        // 选择最长的语音段（最可能是单人连续说话）
        var best = speechSegments.OrderByDescending(s => s.Duration).First();

        // 限制最大提取时长
        var maxSeconds = requirements.MaxDuration.TotalSeconds;
        if (best.Duration.TotalSeconds > maxSeconds)
        {
            best = new SpeechSegment(best.Start, TimeSpan.FromSeconds(best.Start.TotalSeconds + maxSeconds));
        }

        _logger.LogInformation("选取最佳语音段: {Start:F1}s ~ {End:F1}s (时长={Duration:F1}s)",
            best.Start.TotalSeconds, best.End.TotalSeconds, best.Duration.TotalSeconds);

        // 如果最佳片段都不够长，给明确提示
        if (best.Duration < requirements.MinDuration)
        {
            throw new InvalidOperationException(
                $"音频中最长的连续人声片段仅有 {best.Duration.TotalSeconds:F1} 秒，" +
                $"不满足平台要求的最低 {requirements.MinDuration.TotalSeconds:F0} 秒。\n" +
                "可能原因：\n" +
                "• 音频中包含多个说话人，各段人声被分割得太短\n" +
                "• 音频中有过多的背景噪音或音乐干扰\n" +
                "• 音频中人声说话不够连续\n\n" +
                "建议：请提供一段只有一个人连续说话至少10秒以上的纯净录音。");
        }

        return best;
    }

    /// <summary>
    /// 解析 FFmpeg silencedetect 的 stderr 输出，提取静音起止时间
    /// </summary>
    private List<(double Start, double End)> ParseSilenceDetectOutput(List<string> stderrLines)
    {
        var intervals = new List<(double Start, double End)>();
        double? currentStart = null;

        // 匹配: [silencedetect @ ...] silence_start: 1.234
        // 匹配: [silencedetect @ ...] silence_end: 5.678 | silence_duration: 4.444
        var startRegex = new Regex(@"silence_start:\s*([\d.]+)", RegexOptions.Compiled);
        var endRegex = new Regex(@"silence_end:\s*([\d.]+)", RegexOptions.Compiled);

        foreach (var line in stderrLines)
        {
            var startMatch = startRegex.Match(line);
            if (startMatch.Success)
            {
                currentStart = double.Parse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            var endMatch = endRegex.Match(line);
            if (endMatch.Success && currentStart.HasValue)
            {
                var end = double.Parse(endMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                intervals.Add((currentStart.Value, end));
                currentStart = null;
            }
        }

        return intervals;
    }

    /// <summary>
    /// 根据静音区间，反向推算出有声（语音）区间
    /// </summary>
    private static List<SpeechSegment> ExtractSpeechSegments(
        List<(double Start, double End)> silenceIntervals,
        double totalDuration)
    {
        var segments = new List<SpeechSegment>();
        double cursor = 0;

        foreach (var (silStart, silEnd) in silenceIntervals.OrderBy(s => s.Start))
        {
            if (silStart > cursor + 0.1) // 至少 0.1 秒的有效语音
            {
                segments.Add(new SpeechSegment(
                    TimeSpan.FromSeconds(cursor),
                    TimeSpan.FromSeconds(silStart)));
            }
            cursor = silEnd;
        }

        // 末尾可能还有语音
        if (totalDuration > cursor + 0.1)
        {
            segments.Add(new SpeechSegment(
                TimeSpan.FromSeconds(cursor),
                TimeSpan.FromSeconds(totalDuration)));
        }

        // 如果没有检测到静音（整段都是有声），整体作为一段
        if (silenceIntervals.Count == 0 && totalDuration > 0.1)
        {
            segments.Add(new SpeechSegment(TimeSpan.Zero, TimeSpan.FromSeconds(totalDuration)));
        }

        return segments;
    }

    /// <summary>
    /// 根据需求构建 FFmpeg 音频滤波器链
    /// </summary>
    private static string BuildFilterChain(VoiceCloneRequirements requirements)
    {
        var filters = new List<string>();

        if (requirements.EnableDenoise)
        {
            filters.Add("afftdn=nf=-25");
        }

        if (requirements.EnableSilenceRemove)
        {
            filters.Add("silenceremove=stop_periods=-1:stop_duration=1.5:stop_threshold=-35dB");
        }

        return string.Join(",", filters);
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理临时文件失败: {Path}", path);
        }
    }

    /// <summary>语音段（起止时间）</summary>
    private record SpeechSegment(TimeSpan Start, TimeSpan End)
    {
        public TimeSpan Duration => End - Start;
    }
}
