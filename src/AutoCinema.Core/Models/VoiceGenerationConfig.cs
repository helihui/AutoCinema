namespace AutoCinema.Pro.Models;

/// <summary>
/// 语音生成配置
/// </summary>
public record VoiceGenerationConfig
{
    /// <summary>声音 ID</summary>
    public required string VoiceId { get; init; }

    /// <summary>语速 (0.5 - 2.0)</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>音调 (-12 - 12)</summary>
    public int Pitch { get; init; } = 0;

    /// <summary>情感 (neutral, happy, sad, angry, surprised)</summary>
    public string? Emotion { get; init; }
}
