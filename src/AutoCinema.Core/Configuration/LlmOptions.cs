namespace AutoCinema.Pro.Configuration;

/// <summary>
/// LLM 配置选项
/// </summary>
public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>LLM 提供商 (Volcengine, OpenAI)</summary>
    public required string Provider { get; set; }

    /// <summary>API 密钥</summary>
    public required string ApiKey { get; set; }

    /// <summary>模型名称</summary>
    public string Model { get; set; } = "doubao-seed-1-6-251015";

    /// <summary>API 端点</summary>
    public string Endpoint { get; set; } = "https://ark.cn-beijing.volces.com/api/v3/responses";

    /// <summary>温度参数 (0-1)</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>最大 Token 数</summary>
    public int MaxTokens { get; set; } = 4000;
}
