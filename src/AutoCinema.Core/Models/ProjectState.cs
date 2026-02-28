using System.ComponentModel.DataAnnotations;
using AutoCinema.Pro.Pipeline;

namespace AutoCinema.Pro.Models;

/// <summary>
/// 项目运行状态
/// </summary>
public enum ProjectStatus
{
    Pending,
    Processing,
    /// <summary>CoDesign 模式：剧本已生成，等待用户审阅确认</summary>
    WaitingForReview,
    Completed,
    /// <summary>部分成功：剧本生成成功但字幕/时间轴等后续步骤失败，可导出降级版</summary>
    Partial,
    Failed
}

/// <summary>
/// 项目全状态跟踪
/// </summary>
public record ProjectState
{
    [Key]
    public required string ProjectId { get; init; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public JobType Type { get; set; } = JobType.ScriptToVideo;
    public ProjectStatus Status { get; set; } = ProjectStatus.Pending;
    public ProductionProgress? Progress { get; set; }
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}
