using AutoCinema.Pro.Pipeline;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// Pipeline 步骤接口
/// </summary>
/// <typeparam name="TInput">输入数据类型</typeparam>
/// <typeparam name="TOutput">输出数据类型</typeparam>
/// <remarks>
/// 通过强类型泛型确保步骤间的数据流在编译时就能验证正确性
/// </remarks>
public interface IPipelineStep<TInput, TOutput>
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// 判断是否应该执行此步骤(条件跳过)
    /// </summary>
    /// <param name="input">输入数据</param>
    /// <param name="context">执行上下文</param>
    /// <returns>true 表示应该执行,false 表示跳过</returns>
    Task<bool> ShouldExecuteAsync(TInput input, PipelineStepContext context);

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="input">输入数据</param>
    /// <param name="context">执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>输出数据</returns>
    Task<TOutput> ExecuteAsync(TInput input, PipelineStepContext context, CancellationToken ct);

    /// <summary>
    /// 步骤失败时是否可以重试
    /// </summary>
    bool CanRetry { get; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    int MaxRetries { get; }
}
