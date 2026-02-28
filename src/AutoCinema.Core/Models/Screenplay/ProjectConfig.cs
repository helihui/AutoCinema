namespace AutoCinema.Pro.Models.Screenplay;

/// <summary>
/// 项目全局配置（作用于整条剧本生成流水线）
/// </summary>
public record ProjectConfig
{
    /// <summary>画幅比例，如 "16:9" "9:16" "1:1"</summary>
    public string AspectRatio { get; init; } = "16:9";

    /// <summary>
    /// 目标总时长（秒）。LLM 据此分配每个镜头的时长。
    /// 所有镜头时长之和 ≤ TotalDurationSeconds × 0.9（留 10% 给转场）。
    /// </summary>
    public int TotalDurationSeconds { get; init; } = 60;

    /// <summary>
    /// 镜头数量软上限（可选）。
    /// 不填则由 LLM 根据内容自决；填写后若超出则自动合并相邻短镜头。
    /// </summary>
    public int? MaxShotCount { get; init; }

    /// <summary>风格主题，如 "古风水墨" "赛博朋克" "写实电影"</summary>
    public string StyleTheme { get; init; } = "";

    /// <summary>色调，如 "暖金色" "冷蓝调" "高饱和"</summary>
    public string ColorTone { get; init; } = "";

    /// <summary>字幕字体</summary>
    public string SubtitleFont { get; init; } = "思源宋体";

    /// <summary>配音人设 ID（对应 VoiceGenerationConfig.VoiceId）</summary>
    public string NarratorId { get; init; } = "";
}
