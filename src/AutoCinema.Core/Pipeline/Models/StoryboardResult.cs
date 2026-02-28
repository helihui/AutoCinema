using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Pipeline.Models;

/// <summary>
/// 故事板解析结果
/// </summary>
public class StoryboardResult
{
    /// <summary>
    /// 解析后的故事板
    /// </summary>
    public required Storyboard Storyboard { get; set; }

    /// <summary>
    /// 场景数量
    /// </summary>
    public int SceneCount { get; set; }

    /// <summary>
    /// 原始剧本 JSON 数据（透传给 Pipeline 存入 Project）
    /// </summary>
    public string? ScreenplayRawData { get; set; }
}
