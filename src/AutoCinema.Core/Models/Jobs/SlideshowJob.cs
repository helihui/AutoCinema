using System.Text.Json.Serialization;

namespace AutoCinema.Pro.Models.Jobs;

/// <summary>
/// 幻灯片任务项
/// </summary>
public class SlideItem
{
    /// <summary>图片路径</summary>
    public required string ImagePath { get; init; }

    /// <summary>音频路径(可选)</summary>
    public string? AudioPath { get; init; }

    /// <summary>持续时间(秒,若有音频则优先使用音频时长)</summary>
    public double Duration { get; init; } = 3.0;
}

/// <summary>
/// 幻灯片生成任务 (图片转视频)
/// </summary>
public class SlideshowJob : JobItem
{
    public SlideshowJob()
    {
        Type = JobType.ImageToVideo;
    }

    /// <summary>幻灯片列表</summary>
    public List<SlideItem> Items { get; init; } = new();

    /// <summary>背景音乐路径(可选,将覆盖单页音频)</summary>
    public string? BackgroundMusicPath { get; init; }
}
