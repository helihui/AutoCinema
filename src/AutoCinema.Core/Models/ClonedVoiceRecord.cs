using System;

namespace AutoCinema.Pro.Models;

/// <summary>
/// 克隆声音数据库记录
/// </summary>
public class ClonedVoiceRecord
{
    /// <summary>作为主键的声音 ID</summary>
    public required string VoiceId { get; set; }

    /// <summary>显示名称</summary>
    public required string DisplayName { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
