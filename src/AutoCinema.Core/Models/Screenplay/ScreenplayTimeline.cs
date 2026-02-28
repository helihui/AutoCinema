namespace AutoCinema.Pro.Models.Screenplay;

/// <summary>
/// 剧本时间轴（用于视频剪辑软件导入）
/// </summary>
public record ScreenplayTimeline
{
    /// <summary>各镜头的时间轴条目，按镜头序号排序</summary>
    public List<TimelineEntry> Entries { get; init; } = [];

    /// <summary>总时长（秒），所有 Entry 的 OutPoint 最大值</summary>
    public double TotalDurationSeconds => Entries.Count > 0 ? Entries.Max(e => e.OutPoint) : 0;
}

/// <summary>
/// 单个镜头时间轴条目
/// </summary>
public record TimelineEntry
{
    /// <summary>对应 Shot.Index</summary>
    public int ShotIndex { get; init; }

    /// <summary>入点（秒），相对于整部视频起点</summary>
    public double InPoint { get; init; }

    /// <summary>出点（秒）</summary>
    public double OutPoint { get; init; }

    /// <summary>该镜头时长（秒）</summary>
    public double Duration => OutPoint - InPoint;

    /// <summary>配音/音效文件路径（如有）</summary>
    public string? AudioCue { get; init; }

    /// <summary>字幕文本（对应 Shot.NarrationText 的该段）</summary>
    public string SubtitleText { get; init; } = "";

    /// <summary>出场转场方式</summary>
    public string Transition { get; init; } = "切换";

    /// <summary>AIGC 生成的视频/图片文件路径（合成后填入）</summary>
    public string? AssetPath { get; init; }
}
