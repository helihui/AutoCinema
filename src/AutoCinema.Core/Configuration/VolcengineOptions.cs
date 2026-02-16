namespace AutoCinema.Pro.Configuration;

/// <summary>
/// 火山引擎 Volcengine Ark 配置选项
/// </summary>
public class VolcengineOptions
{
    public const string SectionName = "Volcengine";

    /// <summary>API 密钥</summary>
    public required string ApiKey { get; set; }

    /// <summary>API 端点</summary>
    public required string Endpoint { get; set; }

    /// <summary>图片生成模型</summary>
    public string Model { get; set; } = "doubao-seedream-4-5-251128";

    /// <summary>随机种子（用于风格一致性，可选）</summary>
    public int? Seed { get; set; }

    /// <summary>最大并发请求数</summary>
    public int MaxConcurrency { get; set; } = 3;

    /// <summary>图片尺寸 (1K, 2K, 4K)</summary>
    public string ImageSize { get; set; } = "2K";

    /// <summary>响应格式 (url 或 b64_json)</summary>
    public string ResponseFormat { get; set; } = "url";

    /// <summary>是否添加水印</summary>
    public bool Watermark { get; set; } = true;

    /// <summary>是否启用流式传输</summary>
    public bool Stream { get; set; } = false;

    /// <summary>顺序图片生成 (enabled 或 disabled)</summary>
    public string SequentialImageGeneration { get; set; } = "disabled";
}
