namespace AutoCinema.Pro.Models;

/// <summary>
/// 场景生成的素材集合
/// </summary>
public record GeneratedAsset
{
    /// <summary>场景序号</summary>
    public required int SceneIndex { get; init; }

    /// <summary>生成的图片文件路径</summary>
    public required string ImagePath { get; init; }

    /// <summary>生成的音频文件路径</summary>
    public required string AudioPath { get; init; }

    /// <summary>音频精确时长（作为视频时间轴基准）</summary>
    public required TimeSpan AudioDuration { get; init; }

    /// <summary>台词文本（用于字幕生成）</summary>
    public required string SpeechText { get; init; }
}
