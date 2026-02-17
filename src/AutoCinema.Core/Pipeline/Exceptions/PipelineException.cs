namespace AutoCinema.Pro.Pipeline.Exceptions;

/// <summary>
/// Pipeline 异常基类
/// </summary>
public class PipelineException : Exception
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    public string? StepName { get; }

    public PipelineException(string message)
        : base(message)
    {
    }

    public PipelineException(string stepName, string message)
        : base(message)
    {
        StepName = stepName;
    }

    public PipelineException(string stepName, string message, Exception innerException)
        : base(message, innerException)
    {
        StepName = stepName;
    }
}
