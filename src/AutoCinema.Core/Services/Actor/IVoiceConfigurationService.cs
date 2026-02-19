namespace AutoCinema.Pro.Services.Actor;

/// <summary>
/// 声音配置服务接口
/// </summary>
public interface IVoiceConfigurationService
{
    /// <summary>
    /// 获取可用的声音列表
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>声音列表</returns>
    Task<List<Models.VoiceProfile>> GetAvailableVoicesAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取声音详情
    /// </summary>
    /// <param name="voiceId">声音 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>声音详情</returns>
    Task<Models.VoiceProfile?> GetVoiceDetailsAsync(string voiceId, CancellationToken ct = default);

    /// <summary>
    /// 预览声音(生成示例音频)
    /// </summary>
    /// <param name="voiceId">声音 ID</param>
    /// <param name="sampleText">示例文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>音频文件路径</returns>
    Task<string> PreviewVoiceAsync(string voiceId, string sampleText, CancellationToken ct = default);

    /// <summary>
    /// 克隆声音(如果支持)
    /// </summary>
    /// <param name="name">声音名称</param>
    /// <param name="audioFilePath">音频文件路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>克隆的声音配置</returns>
    Task<Models.VoiceProfile> CloneVoiceAsync(string name, string audioFilePath, CancellationToken ct = default);

    /// <summary>
    /// 检查是否支持声音克隆
    /// </summary>
    bool SupportsVoiceCloning { get; }
}
