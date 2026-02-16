namespace AutoCinema.Pro.Configuration;

/// <summary>
/// 流水线配置选项
/// </summary>
public class PipelineOptions
{
    public const string SectionName = "Pipeline";

    /// <summary>默认视觉风格</summary>
    public string DefaultVisualStyle { get; set; } = "Cinematic, high quality, detailed, professional lighting";

    /// <summary>默认角色/主体描述 (用于保持一致性)</summary>
    public string DefaultCharacterPrompt { get; set; } = string.Empty;

    /// <summary>临时文件目录</summary>
    public string TempDirectory { get; set; } = "./temp";

    /// <summary>输出目录</summary>
    public string OutputDirectory { get; set; } = "./output";

    /// <summary>视频帧率</summary>
    public int FrameRate { get; set; } = 24;

    /// <summary>视频编码质量 (CRF值，越小质量越高)</summary>
    public int VideoQuality { get; set; } = 23;

    /// <summary>FFmpeg 可执行文件目录（相对于运行目录或绝对路径）</summary>
    public string? FFmpegDirectory { get; set; }

    /// <summary>示例项目配置</summary>
    public DemoProjectOptions DemoProject { get; set; } = new();
}

public class DemoProjectOptions
{
    public string Title { get; set; } = "Demo Project";
    public string StoryText { get; set; } = "This is a demo story.";
}
