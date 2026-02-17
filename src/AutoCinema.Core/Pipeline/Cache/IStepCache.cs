namespace AutoCinema.Pro.Pipeline.Cache;

/// <summary>
/// 步骤缓存接口
/// </summary>
/// <typeparam name="TInput">输入类型</typeparam>
/// <typeparam name="TOutput">输出类型</typeparam>
public interface IStepCache<TInput, TOutput>
{
    /// <summary>
    /// 获取缓存结果
    /// </summary>
    /// <param name="stepName">步骤名称</param>
    /// <param name="input">输入数据</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>缓存的输出结果,如果不存在则返回 null</returns>
    Task<TOutput?> GetAsync(string stepName, TInput input, CancellationToken ct);

    /// <summary>
    /// 保存缓存结果
    /// </summary>
    /// <param name="stepName">步骤名称</param>
    /// <param name="input">输入数据</param>
    /// <param name="output">输出结果</param>
    /// <param name="ct">取消令牌</param>
    Task SetAsync(string stepName, TInput input, TOutput output, CancellationToken ct);

    /// <summary>
    /// 清除缓存
    /// </summary>
    /// <param name="stepName">步骤名称,如果为 null 则清除所有缓存</param>
    /// <param name="ct">取消令牌</param>
    Task ClearAsync(string? stepName = null, CancellationToken ct = default);
}
