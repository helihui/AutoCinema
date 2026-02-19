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
    Completed,
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
