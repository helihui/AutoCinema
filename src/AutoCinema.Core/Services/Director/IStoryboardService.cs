using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Services.Director;

/// <summary>
/// 故事板解析服务接口
/// 负责将原始故事文本解析为结构化的场景列表
/// </summary>
public interface IStoryboardService
{
    /// <summary>
    /// 将原始故事文本解析为结构化的故事板
    /// </summary>
    /// <param name="rawText">原始故事文本</param>
    /// <param name="baseVisualStyle">基础视觉风格（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>结构化的故事板</returns>
    Task<Storyboard> ParseAsync(string rawText, string? baseVisualStyle = null, CancellationToken ct = default);
}
