using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Services.Editor;

/// <summary>
/// 视频合成服务接口
/// </summary>
public interface IVideoCompositionService
{
    /// <summary>
    /// 将素材合成为最终视频
    /// </summary>
    /// <param name="assets">生成的素材列表</param>
    /// <param name="subtitlePath">字幕文件路径</param>
    /// <param name="outputPath">输出视频路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的视频文件路径</returns>
    Task<string> ComposeAsync(
        IEnumerable<GeneratedAsset> assets,
        string subtitlePath,
        string outputPath,
        CancellationToken ct = default);
}
