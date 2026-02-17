using System.Text;
using AutoCinema.Pro.Pipeline.Metrics;

namespace AutoCinema.Pro.Pipeline.Visualization;

/// <summary>
/// Pipeline 可视化工具
/// </summary>
public class PipelineVisualizer
{
    /// <summary>
    /// 生成 Mermaid 流程图
    /// </summary>
    public string GenerateFlowDiagram()
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");
        sb.AppendLine("    A[VideoProject] -->|故事板解析| B[StoryboardResult]");
        sb.AppendLine("    B -->|素材聚合| C[AssetGenerationResult]");
        sb.AppendLine("    C -->|字幕生成| D[SubtitleGenerationResult]");
        sb.AppendLine("    D -->|视频合成| E[VideoCompositionResult]");
        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>
    /// 生成执行时序图
    /// </summary>
    public string GenerateTimelineDiagram(List<StepExecutionMetrics> metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine("    title Pipeline 执行时序");
        sb.AppendLine("    dateFormat ss.SSS");
        sb.AppendLine("    section 步骤");

        var startTime = metrics.First().StartTime;
        foreach (var metric in metrics)
        {
            var start = (metric.StartTime - startTime).TotalSeconds;
            var end = metric.EndTime.HasValue
                ? (metric.EndTime.Value - startTime).TotalSeconds
                : start;

            var status = metric.IsSuccess ? "done" : "crit";
            var cache = metric.FromCache ? " (缓存)" : "";

            sb.AppendLine($"    {metric.StepName}{cache} :{status}, {start:000.000}, {end:000.000}");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>
    /// 生成性能分布图 (ASCII)
    /// </summary>
    public string GeneratePerformanceChart(List<StepExecutionMetrics> metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("性能分布:");
        sb.AppendLine("─────────────────────────────────────────────────────────");

        var totalDuration = metrics.Sum(m => m.Duration.TotalSeconds);
        if (totalDuration == 0)
        {
            sb.AppendLine("(无性能数据)");
            return sb.ToString();
        }

        var maxNameLength = metrics.Max(m => m.StepName.Length);

        foreach (var metric in metrics)
        {
            var percentage = (metric.Duration.TotalSeconds / totalDuration) * 100;
            var barLength = (int)(percentage / 10); // 每10%一个方块
            var bar = new string('█', barLength) + new string('░', 10 - barLength);
            var cache = metric.FromCache ? " (缓存)" : "";

            sb.AppendLine(
                $"{metric.StepName.PadRight(maxNameLength)}{cache} " +
                $"[{bar}] {percentage:F1}% ({metric.Duration.TotalSeconds:F2}s)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成完整的可视化报告
    /// </summary>
    public string GenerateFullReport(PipelineExecutionReport report, List<StepExecutionMetrics> metrics)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Pipeline 执行报告");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**总耗时**: {report.TotalDuration:mm\\:ss\\.fff}");
        sb.AppendLine($"**成功步骤**: {report.SuccessfulSteps}/{report.TotalSteps}");

        // 缓存统计
        var cachedSteps = metrics.Count(m => m.FromCache);
        if (cachedSteps > 0)
        {
            sb.AppendLine($"**缓存命中**: {cachedSteps}/{metrics.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("## 流程图");
        sb.AppendLine();
        sb.AppendLine(GenerateFlowDiagram());

        sb.AppendLine();
        sb.AppendLine("## 执行时序");
        sb.AppendLine();
        sb.AppendLine(GenerateTimelineDiagram(metrics));

        sb.AppendLine();
        sb.AppendLine("## 性能分析");
        sb.AppendLine(GeneratePerformanceChart(metrics));

        sb.AppendLine();
        sb.AppendLine("## 详细信息");
        sb.AppendLine();
        sb.AppendLine(report.GenerateReport());

        return sb.ToString();
    }
}
