namespace AutoCinema.Pro.Pipeline.Configuration;

/// <summary>
/// Pipeline 配置
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// 配置名称
    /// </summary>
    public string Name { get; set; } = "默认流程";

    /// <summary>
    /// 配置版本
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// 步骤配置列表
    /// </summary>
    public List<StepConfiguration> Steps { get; set; } = new();
}

/// <summary>
/// 步骤配置
/// </summary>
public class StepConfiguration
{
    /// <summary>
    /// 步骤名称 (对应步骤类名)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否使用缓存
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 超时时间(秒)
    /// </summary>
    public int Timeout { get; set; } = 300;
}
