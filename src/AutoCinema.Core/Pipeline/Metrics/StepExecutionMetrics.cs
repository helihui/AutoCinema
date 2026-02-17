namespace AutoCinema.Pro.Pipeline.Metrics;

/// <summary>
/// 步骤执行指标
/// </summary>
public class StepExecutionMetrics
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    public required string StepName { get; set; }

    /// <summary>
    /// 开始时间 (UTC)
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间 (UTC)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行时长
    /// </summary>
    public TimeSpan Duration => EndTime.HasValue
        ? EndTime.Value - StartTime
        : TimeSpan.Zero;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 错误信息(如果失败)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 是否从缓存获取
    /// </summary>
    public bool FromCache { get; set; }
}
