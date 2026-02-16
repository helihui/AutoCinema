using AutoCinema.Pro.Pipeline.Steps;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline;

/// <summary>
/// 类型安全的 Pipeline 构建器
/// </summary>
/// <typeparam name="TCurrentOutput">当前步骤的输出类型</typeparam>
/// <remarks>
/// 通过泛型约束确保步骤间的数据流类型匹配,错误的对接会导致编译失败
/// </remarks>
public class PipelineBuilder<TCurrentOutput>
{
    private readonly List<Func<PipelineStepContext, CancellationToken, Task<object>>> _steps = new();

    /// <summary>
    /// 添加下一个步骤
    /// </summary>
    /// <typeparam name="TNextOutput">下一个步骤的输出类型</typeparam>
    /// <param name="step">步骤实例</param>
    /// <returns>新的 Builder,输出类型为 TNextOutput</returns>
    /// <remarks>
    /// 编译器会检查 TCurrentOutput 是否匹配 step 的输入类型
    /// </remarks>
    public PipelineBuilder<TNextOutput> AddStep<TNextOutput>(
        IPipelineStep<TCurrentOutput, TNextOutput> step)
    {
        _steps.Add(async (context, ct) =>
        {
            var input = (TCurrentOutput)context.SharedData["LastOutput"];

            // 检查是否应该执行
            if (!await step.ShouldExecuteAsync(input, context))
            {
                context.Logger.LogInformation("跳过步骤: {StepName}", step.StepName);
                return input!; // 返回原始输入
            }

            // 执行步骤(带重试)
            var output = await ExecuteWithRetryAsync(step, input, context, ct);
            context.SharedData["LastOutput"] = output!;
            return output!;
        });

        var newBuilder = new PipelineBuilder<TNextOutput>();
        newBuilder._steps.AddRange(_steps);
        return newBuilder;
    }

    /// <summary>
    /// 执行整个 Pipeline
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>最终输出</returns>
    public async Task<TCurrentOutput> ExecuteAsync(
        PipelineStepContext context,
        CancellationToken ct)
    {
        foreach (var step in _steps)
        {
            await step(context, ct);
        }
        return (TCurrentOutput)context.SharedData["LastOutput"];
    }

    /// <summary>
    /// 带重试的执行逻辑
    /// </summary>
    private async Task<TOutput> ExecuteWithRetryAsync<TInput, TOutput>(
        IPipelineStep<TInput, TOutput> step,
        TInput input,
        PipelineStepContext context,
        CancellationToken ct)
    {
        var retryCount = 0;
        while (true)
        {
            try
            {
                return await step.ExecuteAsync(input, context, ct);
            }
            catch (Exception ex)
            {
                if (!step.CanRetry || retryCount >= step.MaxRetries)
                {
                    context.Logger.LogError(ex, "步骤 {StepName} 失败", step.StepName);
                    throw;
                }

                retryCount++;
                var delaySeconds = Math.Pow(2, retryCount); // 指数退避
                context.Logger.LogWarning(
                    "步骤 {StepName} 失败,正在重试 ({Retry}/{Max}),等待 {Delay}s: {Error}",
                    step.StepName, retryCount, step.MaxRetries, delaySeconds, ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            }
        }
    }
}
