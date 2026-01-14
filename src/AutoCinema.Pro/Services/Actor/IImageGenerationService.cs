namespace AutoCinema.Pro.Services.Actor;

/// <summary>
/// 图片生成服务接口
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// 根据提示词生成图片
    /// </summary>
    /// <param name="prompt">图片描述提示词</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的图片文件路径</returns>
    Task<string> GenerateAsync(string prompt, string outputPath, CancellationToken ct = default);
}
