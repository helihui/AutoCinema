namespace AutoCinema.Pro.Models;

/// <summary>
/// 声音配置模型
/// </summary>
public class VoiceProfile
{
    /// <summary>声音 ID</summary>
    public required string VoiceId { get; set; }

    /// <summary>显示名称</summary>
    public required string DisplayName { get; set; }

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>语言</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>性别</summary>
    public VoiceGender Gender { get; set; }

    /// <summary>年龄段</summary>
    public string? AgeRange { get; set; }

    /// <summary>音色特点(标签)</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>是否为克隆声音</summary>
    public bool IsCloned { get; set; }

    /// <summary>预览音频 URL</summary>
    public string? PreviewUrl { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 声音性别
/// </summary>
public enum VoiceGender
{
    /// <summary>男性</summary>
    Male,

    /// <summary>女性</summary>
    Female,

    /// <summary>中性</summary>
    Neutral
}
