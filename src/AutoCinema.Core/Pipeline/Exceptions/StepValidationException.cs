namespace AutoCinema.Pro.Pipeline.Exceptions;

/// <summary>
/// 步骤输入验证异常
/// </summary>
public class StepValidationException : PipelineException
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string? ParameterName { get; }

    public StepValidationException(
        string stepName,
        string message,
        string? parameterName = null)
        : base(stepName, message)
    {
        ParameterName = parameterName;
    }
}
