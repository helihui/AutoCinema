using AutoCinema.Pro.Models.Audio;

namespace AutoCinema.Pro.Services.Audio;

/// <summary>
/// 音频预处理服务接口 - 为声音克隆准备高质量音频
/// </summary>
public interface IAudioPreprocessorService
{
    /// <summary>
    /// 根据指定的平台规范对音频进行流水线处理（降噪、去静音、裁切、转码）
    /// </summary>
    /// <param name="sourceFilePath">原始音频文件路径</param>
    /// <param name="requirements">目标平台的音频要求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>处理后的音频结果</returns>
    Task<AudioProcessResult> PrepareVoiceCloneAudioAsync(
        string sourceFilePath,
        VoiceCloneRequirements requirements,
        CancellationToken ct = default);
}
