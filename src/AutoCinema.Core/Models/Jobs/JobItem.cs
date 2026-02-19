using System.Text.Json.Serialization;
using AutoCinema.Pro.Pipeline;

namespace AutoCinema.Pro.Models.Jobs;

/// <summary>
/// 任务状态
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// 通用任务基类
/// </summary>
[JsonDerivedType(typeof(TextToVideoJob), typeDiscriminator: "TextOnVideo")]
[JsonDerivedType(typeof(SlideshowJob), typeDiscriminator: "ImageToVideo")]
public abstract class JobItem
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// 关联的外部ID (如 ProjectId)
    /// </summary>
    public string? CorrelationId { get; set; }
    public JobType Type { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? FinishedAt { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }

    [JsonIgnore]
    public ProductionProgress Progress { get; set; } = new() { Stage = "Pending", Step = "Waiting to start", Percentage = 0 };
}
