using System.Text;
using AutoCinema.Pro.Models.Screenplay;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Services.Screenplay;

/// <summary>
/// 字幕导出服务（AC-Node: SubtitleComposer）
/// 将分镜旁白按镜头时间轴生成 SRT 字幕文件
/// </summary>
public class SubtitleExporterService
{
    private readonly ILogger<SubtitleExporterService> _logger;

    public SubtitleExporterService(ILogger<SubtitleExporterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 根据分镜列表生成 SRT 字幕文件并写入磁盘
    /// </summary>
    /// <param name="shots">分镜列表（含 NarrationText 和 DurationSeconds）</param>
    /// <param name="outputPath">SRT 输出路径</param>
    public async Task ExportSrtAsync(List<Shot> shots, string outputPath)
    {
        _logger.LogInformation("导出 SRT 字幕，共 {Count} 个镜头，路径: {Path}", shots.Count, outputPath);

        var sb = new StringBuilder();
        double cursor = 0.0;

        for (int i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            if (string.IsNullOrWhiteSpace(shot.NarrationText)) { cursor += shot.DurationSeconds; continue; }

            double inPoint = cursor;
            double outPoint = cursor + shot.DurationSeconds;
            cursor = outPoint;

            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatSrtTime(inPoint)} --> {FormatSrtTime(outPoint)}");
            sb.AppendLine(shot.NarrationText);
            sb.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
        _logger.LogInformation("SRT 字幕文件已写入: {Path}", outputPath);
    }

    private static string FormatSrtTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }
}
