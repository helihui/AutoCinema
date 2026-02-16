namespace AutoCinema.Pro.Models;

/// <summary>
/// 故事板 - 包含场景列表和全局视觉风格
/// </summary>
public record Storyboard
{
    /// <summary>全局视觉风格前缀 (如 "1990s Anime Style, grain texture")</summary>
    public required string BaseVisualStyle { get; init; }

    /// <summary>场景列表</summary>
    public required List<Scene> Scenes { get; init; }

    /// <summary>计算视频总时长（基于所有素材）</summary>
    public TimeSpan TotalDuration => TimeSpan.Zero; // 在素材生成后计算
}
