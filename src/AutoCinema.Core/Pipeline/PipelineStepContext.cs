using AutoCinema.Pro.Models;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline;

/// <summary>
/// Pipeline 步骤执行上下文
/// </summary>
/// <remarks>
/// 在步骤间传递共享信息,避免重复传递参数
/// </remarks>
public class PipelineStepContext
{
    /// <summary>
    /// 视频项目信息
    /// </summary>
    public required VideoProject Project { get; set; }

    /// <summary>
    /// 进度报告器
    /// </summary>
    public IProgress<ProductionProgress>? Progress { get; set; }

    /// <summary>
    /// 日志记录器
    /// </summary>
    public required ILogger Logger { get; set; }

    /// <summary>
    /// 步骤间共享数据
    /// </summary>
    /// <remarks>
    /// 用于在步骤间传递临时数据,例如中间结果、配置等
    /// </remarks>
    public Dictionary<string, object> SharedData { get; set; } = new();
}
