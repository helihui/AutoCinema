namespace AutoCinema.Pro.Models;

/// <summary>
/// 表示故事中的一个场景
/// </summary>
public record Scene
{
    /// <summary>场景序号 (1-based)</summary>
    public int Index { get; init; }

    /// <summary>台词/旁白文本 (用于 TTS)</summary>
    public required string SpeechText { get; init; }

    /// <summary>视觉画面描述 (用于生图 Prompt)</summary>
    public required string VisualPrompt { get; init; }
}
