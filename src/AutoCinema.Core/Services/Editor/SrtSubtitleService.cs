using System.Text;
using AutoCinema.Pro.Models;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Services.Editor;

/// <summary>
/// SRT 格式字幕生成服务
/// 实现音画对齐的核心算法
/// </summary>
public class SrtSubtitleService : ISubtitleService
{
    private readonly ILogger<SrtSubtitleService> _logger;

    public SrtSubtitleService(ILogger<SrtSubtitleService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成 SRT 格式字幕文件
    /// 
    /// 音画对齐算法：
    /// - 基准确定：视频时长 = 音频时长
    /// - 字幕偏移计算：
    ///   - StartTime_N = Sum(Duration_1 ... Duration_N-1)
    ///   - EndTime_N = StartTime_N + Duration_N
    /// </summary>
    public async Task<string> GenerateSrtAsync(
        IEnumerable<GeneratedAsset> assets,
        string outputPath,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var orderedAssets = assets.OrderBy(a => a.SceneIndex).ToList();
        var currentTime = TimeSpan.Zero;

        _logger.LogInformation("开始生成字幕，共 {Count} 个场景", orderedAssets.Count);

        int srtIndex = 1;

        for (int i = 0; i < orderedAssets.Count; i++)
        {
            var asset = orderedAssets[i];
            var sceneStartTime = currentTime;

            // 1. 拆分文本为短句
            var sentences = SplitTextIntoSentences(asset.SpeechText);

            // 2. 计算总字符数（用于分配时间）
            var totalChars = sentences.Sum(s => s.Length);

            // 如果没有有效字符（极少情况），直接使用整段时长
            if (totalChars == 0)
            {
                totalChars = 1;
                sentences = new List<string> { asset.SpeechText };
            }

            var currentSentenceStartTime = sceneStartTime;

            // 3. 为每个短句分配时间并生成字幕
            for (int j = 0; j < sentences.Count; j++)
            {
                var sentence = sentences[j];

                // 计算该句子的时长比例
                // 最后一句话直接使用剩余时长，避免浮点数误差
                TimeSpan duration;
                if (j == sentences.Count - 1)
                {
                    duration = (sceneStartTime + asset.AudioDuration) - currentSentenceStartTime;
                }
                else
                {
                    var ratio = (double)sentence.Length / totalChars;
                    duration = asset.AudioDuration * ratio;
                }

                var endTime = currentSentenceStartTime + duration;

                // 生成 SRT 条目
                sb.AppendLine((srtIndex++).ToString());
                sb.AppendLine($"{FormatSrtTime(currentSentenceStartTime)} --> {FormatSrtTime(endTime)}");
                sb.AppendLine(sentence);
                sb.AppendLine();

                _logger.LogDebug("字幕 {Index}: {Start} --> {End} | {Text}",
                    srtIndex - 1, FormatSrtTime(currentSentenceStartTime), FormatSrtTime(endTime), sentence);

                currentSentenceStartTime = endTime;
            }

            // 更新总时间偏移
            currentTime += asset.AudioDuration;
        }

        // 确保目录存在
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // 使用 UTF-8 with BOM 以确保兼容性
        await File.WriteAllTextAsync(outputPath, sb.ToString(), new UTF8Encoding(true), ct);

        _logger.LogInformation("字幕生成完成: {Path}，总时长: {Duration:mm\\:ss\\.fff}", outputPath, currentTime);

        return outputPath;
    }

    /// <summary>
    /// 将长文本拆分为短句
    /// </summary>
    private static List<string> SplitTextIntoSentences(string text)
    {
        var sentences = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return sentences;
        }

        // 定义分隔符：句号、问号、感叹号、逗号、分号、冒号（中英文）
        // 这里的逻辑是：遇到分隔符就断句，并将分隔符保留在上一句末尾
        var sb = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            sb.Append(c);

            if (IsSeparator(c))
            {
                // 如果下一个字符是引号或括号，也一并包含进来
                if (i + 1 < text.Length && IsClosingPunctuation(text[i + 1]))
                {
                    continue;
                }

                sentences.Add(sb.ToString().Trim());
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            var remaining = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(remaining))
            {
                sentences.Add(remaining);
            }
        }

        return sentences;
    }

    private static bool IsSeparator(char c)
    {
        return c == '。' || c == '？' || c == '！' || c == '，' || c == '；' || c == '：' ||
               c == '.' || c == '?' || c == '!' || c == ',' || c == ';' || c == ':' ||
               c == '\n' || c == '\r';
    }

    private static bool IsClosingPunctuation(char c)
    {
        return c == '”' || c == '’' || c == '"' || c == '\'' || c == ')' || c == '）' || c == '》';
    }

    /// <summary>
    /// 格式化为 SRT 时间格式：00:00:00,000
    /// </summary>
    private static string FormatSrtTime(TimeSpan ts)
    {
        return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00},{ts.Milliseconds:000}";
    }
}
