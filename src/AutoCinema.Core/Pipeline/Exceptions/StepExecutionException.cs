namespace AutoCinema.Pro.Pipeline.Exceptions;

/// <summary>
/// 步骤执行异常
/// </summary>
public class StepExecutionException : PipelineException
{
    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; }

    public StepExecutionException(
        string stepName,
        string message,
        Exception innerException,
        int retryCount = 0)
        : base(stepName, message, innerException)
    {
        RetryCount = retryCount;
    }
}
