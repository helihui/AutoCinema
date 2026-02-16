using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Pipeline.Models;

/// <summary>
/// 素材生成结果
/// </summary>
public class AssetGenerationResult
{
    /// <summary>
    /// 生成的素材数组
    /// </summary>
    public required GeneratedAsset[] Assets { get; set; }

    /// <summary>
    /// 总时长
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
}
