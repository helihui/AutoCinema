using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Pipeline;

namespace AutoCinema.Core.Services.Jobs;

/// <summary>
/// 通用任务处理器接口
/// </summary>
public interface IJobHandler
{
    /// <summary>
    /// 支持的任务类型
    /// </summary>
    JobType SupportedType { get; }

    /// <summary>
    /// 执行任务
    /// </summary>
    Task ExecuteAsync(JobItem job, IProgress<ProductionProgress> progress, CancellationToken ct);
}
