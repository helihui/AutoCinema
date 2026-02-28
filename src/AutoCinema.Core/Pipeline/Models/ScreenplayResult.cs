using AutoCinema.Pro.Models.Screenplay;

namespace AutoCinema.Pro.Pipeline.Models;

/// <summary>
/// 剧本生成步骤的输出结果
/// </summary>
public class ScreenplayResult
{
    /// <summary>生成的完整剧本（已完成时长兜底和镜头数合并）</summary>
    public required ScreenplayDocument Screenplay { get; set; }

    /// <summary>
    /// CoDesign 模式下的审阅关卡（为 null 表示直接跳过审阅继续执行）。
    /// 非 null 时，下游步骤需等待用户确认后才能获取最终剧本。
    /// </summary>
    public StoryboardReviewGate? ReviewGate { get; set; }

    /// <summary>是否需要用户审阅</summary>
    public bool RequiresReview => ReviewGate != null;

    /// <summary>分镜数量</summary>
    public int ShotCount => Screenplay.Shots.Count;

    /// <summary>预计总时长（秒）</summary>
    public int TotalDurationSeconds => Screenplay.Shots.Sum(s => s.DurationSeconds);
}
