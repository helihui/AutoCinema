namespace AutoCinema.Pro.Services.Editor;

/// <summary>
/// 音频分析服务接口
/// </summary>
public interface IAudioAnalysisService
{
    /// <summary>
    /// 获取音频文件的精确时长
    /// </summary>
    /// <param name="audioPath">音频文件路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>音频时长</returns>
    Task<TimeSpan> GetDurationAsync(string audioPath, CancellationToken ct = default);
}
