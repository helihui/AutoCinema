namespace AutoCinema.Pro.Configuration;

/// <summary>
/// MiniMax TTS 配置选项
/// </summary>
public class MiniMaxOptions
{
    public const string SectionName = "MiniMax";

    /// <summary>API 密钥</summary>
    public required string ApiKey { get; set; }

    /// <summary>API 端点</summary>
    public string Endpoint { get; set; } = "https://api.minimaxi.com/v1/t2a_v2";

    /// <summary>模型名称</summary>
    public string Model { get; set; } = "speech-2.6-hd";

    /// <summary>音色 ID</summary>
    public string VoiceId { get; set; } = "male-qn-qingse";

    /// <summary>语速 (0.5-2.0)</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>音量 (0.1-10.0)</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>音调 (-12 到 12)</summary>
    public int Pitch { get; set; } = 0;

    /// <summary>情感 (happy, sad, angry, fearful, disgusted, surprised)</summary>
    public string? Emotion { get; set; }

    /// <summary>采样率</summary>
    public int SampleRate { get; set; } = 32000;

    /// <summary>比特率</summary>
    public int Bitrate { get; set; } = 128000;

    /// <summary>音频格式 (mp3, wav, pcm, flac)</summary>
    public string Format { get; set; } = "mp3";

    /// <summary>声道数 (1=单声道, 2=立体声)</summary>
    public int Channel { get; set; } = 1;

    /// <summary>是否启用流式传输</summary>
    public bool Stream { get; set; } = false;

    /// <summary>是否启用字幕</summary>
    public bool SubtitleEnable { get; set; } = false;
}
