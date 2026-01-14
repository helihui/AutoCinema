namespace AutoCinema.Pro.Configuration;

/// <summary>
/// 火山引擎 TTS 配置选项
/// </summary>
public class VolcengineTtsOptions
{
    public const string SectionName = "VolcengineTts";

    /// <summary>应用 ID</summary>
    public required string AppId { get; set; }

    /// <summary>访问令牌</summary>
    public required string AccessToken { get; set; }

    /// <summary>集群 ID</summary>
    public required string Cluster { get; set; }

    /// <summary>API 端点</summary>
    public string Endpoint { get; set; } = "https://openspeech.bytedance.com/api/v1/tts";

    /// <summary>音色类型</summary>
    public string VoiceType { get; set; } = "zh_female_qingxin";

    /// <summary>音频编码格式 (mp3, wav, pcm)</summary>
    public string Encoding { get; set; } = "mp3";

    /// <summary>语速比例 (0.5-2.0)</summary>
    public double SpeedRatio { get; set; } = 1.0;

    /// <summary>音量比例 (0.1-3.0)</summary>
    public double VolumeRatio { get; set; } = 1.0;

    /// <summary>音调比例 (0.5-2.0)</summary>
    public double PitchRatio { get; set; } = 1.0;

    /// <summary>用户 UID（可选）</summary>
    public string UserId { get; set; } = "default_user";
}
