namespace AutoCinema.Pro.Models;

/// <summary>
/// 视频项目配置
/// </summary>
public record VideoProject
{
    /// <summary>项目唯一标识</summary>
    public required string ProjectId { get; init; }

    /// <summary>任务类型</summary>
    public JobType Type { get; init; } = JobType.ScriptToVideo;

    /// <summary>视频标题</summary>
    public required string Title { get; init; }

    /// <summary>输出目录</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>原始故事文本</summary>
    public required string RawStoryText { get; init; }

    /// <summary>基础视觉风格(可选,用于风格一致性)</summary>
    public string? BaseVisualStyle { get; init; }

    /// <summary>角色/主体描述(可选,用于保持角色一致性)</summary>
    public string? CharacterPrompt { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>语音生成配置(可选,用于指定声音参数)</summary>
    public VoiceGenerationConfig? VoiceConfig { get; init; }
}
