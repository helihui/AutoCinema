namespace AutoCinema.Pro.Services.Actor;

/// <summary>
/// 语音合成服务接口
/// </summary>
public interface ISpeechGenerationService
{
    /// <summary>
    /// 将文本转换为语音
    /// </summary>
    /// <param name="text">要转换的文本</param>
    /// <param name="outputPath">输出音频文件路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的音频文件路径</returns>
    Task<string> GenerateAsync(string text, string outputPath, CancellationToken ct = default);
}
