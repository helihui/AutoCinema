using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoCinema.Pro.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Services.Director;

/// <summary>
/// 火山引擎 LLM 客户端服务
/// </summary>
public class VolcengineLlmService
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<VolcengineLlmService> _logger;

    public VolcengineLlmService(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<VolcengineLlmService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 发送聊天请求并获取响应
    /// </summary>
    public async Task<string> GetResponseAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        // 火山引擎不支持 system role，将 system prompt 合并到 user prompt 中
        var combinedPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var requestBody = new VolcengineLlmRequest
        {
            Model = _options.Model,
            Input = new List<VolcengineLlmMessage>
            {
                new()
                {
                    Role = "user",
                    Content = new List<VolcengineLlmContent>
                    {
                        new() { Type = "input_text", Text = combinedPrompt }
                    }
                }
            },
            Thinking = new VolcengineLlmThinking
            {
                Type = "disabled"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(requestBody, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        _logger.LogDebug("发送火山引擎 LLM 请求: {Model}", _options.Model);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<VolcengineLlmResponse>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
            ct);

        if (result?.Output == null || result.Output.Count == 0)
        {
            throw new InvalidOperationException("火山引擎 LLM 返回了空的响应");
        }

        var outputMessage = result.Output[0];
        if (outputMessage.Content == null || outputMessage.Content.Count == 0)
        {
            throw new InvalidOperationException("火山引擎 LLM 返回的消息内容为空");
        }

        // 提取文本内容
        var textContent = outputMessage.Content
            .Where(c => c.Type == "output_text" && !string.IsNullOrEmpty(c.Text))
            .Select(c => c.Text)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(textContent))
        {
            throw new InvalidOperationException("火山引擎 LLM 返回的文本内容为空");
        }

        _logger.LogDebug("收到火山引擎 LLM 响应，长度: {Length}", textContent.Length);

        return textContent;
    }
}

#region API Request/Response Models

internal class VolcengineLlmRequest
{
    public required string Model { get; set; }
    public required List<VolcengineLlmMessage> Input { get; set; }
    public VolcengineLlmThinking? Thinking { get; set; }
}

internal class VolcengineLlmMessage
{
    public required string Role { get; set; }
    public required List<VolcengineLlmContent> Content { get; set; }
}

internal class VolcengineLlmContent
{
    public required string Type { get; set; }
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}

internal class VolcengineLlmThinking
{
    public required string Type { get; set; }
}

internal class VolcengineLlmResponse
{
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    public string? Id { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public int MaxOutputTokens { get; set; }

    public string? Model { get; set; }

    public string? Object { get; set; }

    public List<VolcengineLlmOutputMessage>? Output { get; set; }

    public VolcengineLlmThinking? Thinking { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    public string? Status { get; set; }

    public VolcengineLlmUsage? Usage { get; set; }
}

internal class VolcengineLlmOutputMessage
{
    public string? Type { get; set; }
    public string? Role { get; set; }
    public List<VolcengineLlmContent>? Content { get; set; }
    public string? Status { get; set; }
    public string? Id { get; set; }
}

internal class VolcengineLlmUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

#endregion
