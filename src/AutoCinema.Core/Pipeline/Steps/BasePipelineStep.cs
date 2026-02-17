using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Pipeline.Exceptions;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// Pipeline 步骤抽象基类
/// </summary>
/// <typeparam name="TInput">输入数据类型</typeparam>
/// <typeparam name="TOutput">输出数据类型</typeparam>
/// <remarks>
/// 提供默认实现,减少重复代码
/// </remarks>
public abstract class BasePipelineStep<TInput, TOutput> : IPipelineStep<TInput, TOutput>
{
    /// <inheritdoc/>
    public abstract string StepName { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// 默认总是执行,子类可以重写实现条件跳过逻辑
    /// </remarks>
    public virtual Task<bool> ShouldExecuteAsync(TInput input, PipelineStepContext context)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public abstract Task<TOutput> ExecuteAsync(TInput input, PipelineStepContext context, CancellationToken ct);

    /// <inheritdoc/>
    /// <remarks>
    /// 默认不支持重试,子类可以重写
    /// </remarks>
    public virtual bool CanRetry => false;

    /// <inheritdoc/>
    /// <remarks>
    /// 默认最多重试 3 次
    /// </remarks>
    public virtual int MaxRetries => 3;

    /// <summary>
    /// 记录步骤开始日志
    /// </summary>
    protected void LogStepStart(PipelineStepContext context)
    {
        context.Logger.LogInformation("开始执行步骤: {StepName}", StepName);
    }

    /// <summary>
    /// 记录步骤完成日志
    /// </summary>
    protected void LogStepComplete(PipelineStepContext context)
    {
        context.Logger.LogInformation("步骤完成: {StepName}", StepName);
    }

    /// <summary>
    /// 报告进度
    /// </summary>
    protected void ReportProgress(PipelineStepContext context, string stage, string step, int percentage)
    {
        context.Progress?.Report(new ProductionProgress
        {
            Stage = stage,
            Step = step,
            Percentage = percentage
        });
    }

    /// <summary>
    /// 验证输入
    /// </summary>
    /// <remarks>
    /// 子类可以重写此方法添加特定的验证逻辑
    /// </remarks>
    protected virtual void ValidateInput(TInput input)
    {
        if (input == null)
        {
            throw new StepValidationException(
                StepName,
                "输入不能为 null",
                nameof(input));
        }
    }
}
