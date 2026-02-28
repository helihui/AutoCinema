namespace AutoCinema.Pro.Models.Screenplay;

/// <summary>
/// 角色锚定卡（CharacterAnchor）
/// 用于在整个剧本生成过程中保持角色外观的一致性。
/// 每次生成分镜提示词时，将该卡的核心词注入到 AIGC Prompt 中。
/// </summary>
public record CharacterAnchor
{
    /// <summary>角色名</summary>
    public string CharacterName { get; init; } = "";

    /// <summary>核心外观关键词（每次生图必须包含），如 "白发，金眸，红色汉服，身姿挺拔"</summary>
    public string[] CoreKeywords { get; init; } = [];

    /// <summary>禁用词（生图时应排除），如 "黑发，蓝眼"</summary>
    public string[] ForbiddenWords { get; init; } = [];

    /// <summary>角色参考图本地路径（如有 IP 图稿）</summary>
    public string? ReferenceImagePath { get; init; }

    /// <summary>将核心词拼接为提示词片段</summary>
    public string ToPromptSnippet() =>
        CoreKeywords.Length > 0 ? string.Join(", ", CoreKeywords) : "";
}
