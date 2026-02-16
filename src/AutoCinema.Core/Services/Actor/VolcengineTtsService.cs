using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace AutoCinema.Pro.Services.Actor;

/// <summary>
/// 火山引擎 TTS 语音合成服务
/// </summary>
public class VolcengineTtsService : ISpeechGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly VolcengineTtsOptions _options;
    private readonly ILogger<VolcengineTtsService> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public VolcengineTtsService(
        HttpClient httpClient,
        IOptions<VolcengineTtsOptions> options,
        ILogger<VolcengineTtsService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _retryPolicy = PollyPolicies.GetSpeechGenerationPolicy();
    }

    public async Task<string> GenerateAsync(string text, string outputPath, CancellationToken ct = default)
    {
        _logger.LogDebug("开始生成语音: {Text}", text[..Math.Min(30, text.Length)] + "...");

        var requestBody = new VolcengineTtsRequest
        {
            App = new VolcengineTtsApp
            {
                AppId = _options.AppId,
                Token = "access_token",
                Cluster = _options.Cluster
            },
            User = new VolcengineTtsUser
            {
                Uid = _options.UserId
            },
            Audio = new VolcengineTtsAudio
            {
                VoiceType = _options.VoiceType,
                Encoding = _options.Encoding,
                SpeedRatio = _options.SpeedRatio,
                VolumeRatio = _options.VolumeRatio,
                PitchRatio = _options.PitchRatio
            },
            Request = new VolcengineTtsRequestInfo
            {
                ReqId = Guid.NewGuid().ToString(),
                Text = text,
                TextType = "plain",
                Operation = "query",
                WithFrontend = 1,
                FrontendType = "unitTson"
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        var response = await _retryPolicy.ExecuteAsync(
            async (context, cancellation) =>
            {
                using var req = CloneRequest(request);
                return await _httpClient.SendAsync(req, cancellation);
            },
            new Context(),
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<VolcengineTtsResponse>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
            ct);

        if (result?.Data == null || string.IsNullOrEmpty(result.Data))
        {
            throw new InvalidOperationException("火山引擎 TTS 返回了空的音频数据");
        }

        var audioData = Convert.FromBase64String(result.Data);

        // 确保目录存在
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(outputPath, audioData, ct);

        _logger.LogInformation("语音生成成功: {Path}", outputPath);
        return outputPath;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content != null)
        {
            var content = original.Content.ReadAsStringAsync().Result;
            clone.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
        }
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}

#region API Request/Response Models

internal class VolcengineTtsRequest
{
    public required VolcengineTtsApp App { get; set; }
    public required VolcengineTtsUser User { get; set; }
    public required VolcengineTtsAudio Audio { get; set; }
    public required VolcengineTtsRequestInfo Request { get; set; }
}

internal class VolcengineTtsApp
{
    [JsonPropertyName("appid")]
    public required string AppId { get; set; }
    
    public required string Token { get; set; }
    public required string Cluster { get; set; }
}

internal class VolcengineTtsUser
{
    public required string Uid { get; set; }
}

internal class VolcengineTtsAudio
{
    [JsonPropertyName("voice_type")]
    public required string VoiceType { get; set; }
    
    public required string Encoding { get; set; }
    
    [JsonPropertyName("speed_ratio")]
    public double SpeedRatio { get; set; }
    
    [JsonPropertyName("volume_ratio")]
    public double VolumeRatio { get; set; }
    
    [JsonPropertyName("pitch_ratio")]
    public double PitchRatio { get; set; }
}

internal class VolcengineTtsRequestInfo
{
    [JsonPropertyName("reqid")]
    public required string ReqId { get; set; }
    
    public required string Text { get; set; }
    
    [JsonPropertyName("text_type")]
    public required string TextType { get; set; }
    
    public required string Operation { get; set; }
    
    [JsonPropertyName("with_frontend")]
    public int WithFrontend { get; set; }
    
    [JsonPropertyName("frontend_type")]
    public required string FrontendType { get; set; }
}

internal class VolcengineTtsResponse
{
    public string? Data { get; set; }
    public int? Code { get; set; }
    public string? Message { get; set; }
}

#endregion
