using System.Text;

namespace AutoCinema.Pro.Pipeline.Metrics;

/// <summary>
/// Pipeline 执行报告
/// </summary>
public class PipelineExecutionReport
{
    private readonly List<StepExecutionMetrics> _stepMetrics;

    public PipelineExecutionReport(List<StepExecutionMetrics> stepMetrics)
    {
        _stepMetrics = stepMetrics;
    }

    /// <summary>
    /// 总执行时间
    /// </summary>
    public TimeSpan TotalDuration => _stepMetrics
        .Where(m => m.EndTime.HasValue)
        .Select(m => m.Duration)
        .Aggregate(TimeSpan.Zero, (sum, duration) => sum + duration);

    /// <summary>
    /// 成功步骤数
    /// </summary>
    public int SuccessfulSteps => _stepMetrics.Count(m => m.IsSuccess);

    /// <summary>
    /// 失败步骤数
    /// </summary>
    public int FailedSteps => _stepMetrics.Count(m => !m.IsSuccess);

    /// <summary>
    /// 总步骤数
    /// </summary>
    public int TotalSteps => _stepMetrics.Count;

    /// <summary>
    /// 生成格式化的性能报告
    /// </summary>
    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("╔════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              Pipeline 性能报告                         ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"总执行时间: {TotalDuration:mm\\:ss\\.fff}");
        sb.AppendLine($"成功步骤: {SuccessfulSteps}/{TotalSteps}");

        if (FailedSteps > 0)
        {
            sb.AppendLine($"失败步骤: {FailedSteps}");
        }

        sb.AppendLine();
        sb.AppendLine("步骤详情:");
        sb.AppendLine("─────────────────────────────────────────────────────────");

        for (int i = 0; i < _stepMetrics.Count; i++)
        {
            var metric = _stepMetrics[i];
            var status = metric.IsSuccess ? "✅ 成功" : "❌ 失败";
            var cacheInfo = metric.FromCache ? " (使用缓存)" : "";
            var retryInfo = metric.RetryCount > 0 ? $", 重试 {metric.RetryCount} 次" : "";

            sb.AppendLine($"步骤 {i + 1}: {metric.StepName}");
            sb.AppendLine($"  状态: {status}{cacheInfo}");
            sb.AppendLine($"  耗时: {metric.Duration.TotalSeconds:F2} 秒{retryInfo}");

            if (!string.IsNullOrEmpty(metric.ErrorMessage))
            {
                sb.AppendLine($"  错误: {metric.ErrorMessage}");
            }

            sb.AppendLine();
        }

        // 性能瓶颈分析
        var bottlenecks = GetBottlenecks(3);
        if (bottlenecks.Any())
        {
            sb.AppendLine("性能瓶颈 (Top 3):");
            sb.AppendLine("─────────────────────────────────────────────────────────");

            foreach (var (metric, percentage) in bottlenecks)
            {
                sb.AppendLine($"• {metric.StepName}: {metric.Duration:mm\\:ss\\.fff} ({percentage:F1}%)");
            }
        }

        sb.AppendLine("═════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// 获取耗时最长的步骤
    /// </summary>
    public IEnumerable<(StepExecutionMetrics Metric, double Percentage)> GetBottlenecks(int topN = 3)
    {
        if (TotalDuration == TimeSpan.Zero)
        {
            return Enumerable.Empty<(StepExecutionMetrics, double)>();
        }

        return _stepMetrics
            .Where(m => m.IsSuccess && m.EndTime.HasValue)
            .OrderByDescending(m => m.Duration)
            .Take(topN)
            .Select(m => (m, (m.Duration.TotalMilliseconds / TotalDuration.TotalMilliseconds) * 100));
    }
}
