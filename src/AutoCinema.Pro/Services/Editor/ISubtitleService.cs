using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Services.Editor;

/// <summary>
/// 字幕生成服务接口
/// </summary>
public interface ISubtitleService
{
    /// <summary>
    /// 生成 SRT 格式字幕文件
    /// </summary>
    /// <param name="assets">生成的素材列表（包含时长和台词）</param>
    /// <param name="outputPath">输出字幕文件路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的字幕文件路径</returns>
    Task<string> GenerateSrtAsync(IEnumerable<GeneratedAsset> assets, string outputPath, CancellationToken ct = default);
}
