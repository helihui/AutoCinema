namespace AutoCinema.Pro.Models.Audio;

/// <summary>
/// 各平台对声音克隆音频的硬性要求规范
/// </summary>
public class VoiceCloneRequirements
{
    /// <summary>最低时长要求</summary>
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>最大时长限制</summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>文件大小限制（字节）</summary>
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024; // 20MB

    /// <summary>目标格式（mp3, wav, m4a）</summary>
    public string TargetFormat { get; set; } = "mp3";

    /// <summary>目标采样率（Hz）</summary>
    public int TargetSampleRate { get; set; } = 32000;

    /// <summary>是否开启降噪</summary>
    public bool EnableDenoise { get; set; } = true;

    /// <summary>是否开启去除静音</summary>
    public bool EnableSilenceRemove { get; set; } = true;

    /// <summary>MiniMax 平台的默认要求</summary>
    public static VoiceCloneRequirements MiniMax => new()
    {
        MinDuration = TimeSpan.FromSeconds(10),
        MaxDuration = TimeSpan.FromMinutes(5),
        MaxFileSizeBytes = 20 * 1024 * 1024,
        TargetFormat = "mp3",
        TargetSampleRate = 32000,
        EnableDenoise = true,
        EnableSilenceRemove = true
    };
}
