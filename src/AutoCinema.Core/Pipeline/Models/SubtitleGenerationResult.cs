using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Pipeline.Models;

/// <summary>
/// 字幕生成结果
/// </summary>
public class SubtitleGenerationResult
{
    /// <summary>
    /// 字幕文件路径
    /// </summary>
    public required string SubtitlePath { get; set; }

    /// <summary>
    /// 字幕数量
    /// </summary>
    public int SubtitleCount { get; set; }

    /// <summary>
    /// 素材数组(传递给下一步)
    /// </summary>
    public required GeneratedAsset[] Assets { get; set; }
}
