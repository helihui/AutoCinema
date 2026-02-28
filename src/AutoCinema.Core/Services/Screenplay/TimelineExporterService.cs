using System.Text.Json;
using AutoCinema.Pro.Models.Screenplay;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Services.Screenplay;

/// <summary>
/// 时间轴导出服务（AC-Node: TimelineExporter）
/// 将分镜列表转换为时间轴 JSON，可导入剪辑软件
/// </summary>
public class TimelineExporterService
{
    private readonly ILogger<TimelineExporterService> _logger;

    public TimelineExporterService(ILogger<TimelineExporterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 根据分镜列表生成时间轴对象
    /// </summary>
    public ScreenplayTimeline BuildTimeline(List<Shot> shots)
    {
        var entries = new List<TimelineEntry>();
        double cursor = 0.0;

        foreach (var shot in shots)
        {
            double inPoint = cursor;
            double outPoint = cursor + shot.DurationSeconds;
            cursor = outPoint;

            entries.Add(new TimelineEntry
            {
                ShotIndex = shot.Index,
                InPoint = Math.Round(inPoint, 3),
                OutPoint = Math.Round(outPoint, 3),
                SubtitleText = shot.NarrationText,
                Transition = shot.Transition,
                AudioCue = null,
                AssetPath = null
            });
        }

        _logger.LogInformation("时间轴生成完成，共 {Count} 条目，总时长={Total}s",
            entries.Count, entries.LastOrDefault()?.OutPoint ?? 0);

        return new ScreenplayTimeline { Entries = entries };
    }

    /// <summary>
    /// 将时间轴序列化为 JSON 并写入磁盘
    /// </summary>
    public async Task ExportJsonAsync(ScreenplayTimeline timeline, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var json = JsonSerializer.Serialize(timeline, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(outputPath, json);
        _logger.LogInformation("时间轴 JSON 已写入: {Path}", outputPath);
    }
}
