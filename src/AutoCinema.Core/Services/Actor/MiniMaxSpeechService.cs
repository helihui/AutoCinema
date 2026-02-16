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
/// MiniMax TTS 语音合成服务
/// </summary>
public class MiniMaxSpeechService : ISpeechGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly MiniMaxOptions _options;
    private readonly ILogger<MiniMaxSpeechService> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public MiniMaxSpeechService(
        HttpClient httpClient,
        IOptions<MiniMaxOptions> options,
        ILogger<MiniMaxSpeechService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _retryPolicy = PollyPolicies.GetSpeechGenerationPolicy();
    }

    public async Task<string> GenerateAsync(string text, string outputPath, CancellationToken ct = default)
    {
        _logger.LogDebug("开始生成语音: {Text}", text[..Math.Min(30, text.Length)] + "...");

        var requestBody = new MiniMaxTtsRequest
        {
            Model = _options.Model,
            Text = text,
            Stream = _options.Stream,
            VoiceSetting = new VoiceSettingDto
            {
                VoiceId = _options.VoiceId,
                Speed = _options.Speed,
                Vol = _options.Volume,
                Pitch = _options.Pitch,
                Emotion = _options.Emotion
            },
            AudioSetting = new AudioSettingDto
            {
                SampleRate = _options.SampleRate,
                Bitrate = _options.Bitrate,
                Format = _options.Format,
                Channel = _options.Channel
            },
            SubtitleEnable = _options.SubtitleEnable
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

        var response = await _retryPolicy.ExecuteAsync(
            async (context, cancellation) =>
            {
                using var req = CloneRequest(request);
                return await _httpClient.SendAsync(req, cancellation);
            },
            new Context(),
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MiniMaxTtsResponse>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
            ct);

        // 检查 MiniMax 的错误响应
        if (result?.BaseResp != null && result.BaseResp.StatusCode != 0)
        {
            var errorMsg = $"MiniMax TTS 错误 (代码: {result.BaseResp.StatusCode}): {result.BaseResp.StatusMsg}";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (result?.Data?.Audio == null || string.IsNullOrEmpty(result.Data.Audio))
        {
            throw new InvalidOperationException("MiniMax 返回了空的音频数据");
        }

        // MiniMax 返回的是 Hex 编码的音频数据，不是 Base64
        var audioData = Convert.FromHexString(result.Data.Audio);

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

internal class MiniMaxTtsRequest
{
    public required string Model { get; set; }
    public required string Text { get; set; }
    public bool Stream { get; set; }
    public required VoiceSettingDto VoiceSetting { get; set; }
    public required AudioSettingDto AudioSetting { get; set; }
    public bool SubtitleEnable { get; set; }
}

internal class VoiceSettingDto
{
    public required string VoiceId { get; set; }
    public double Speed { get; set; } = 1.0;
    public double Vol { get; set; } = 1.0;
    public int Pitch { get; set; } = 0;
    public string? Emotion { get; set; }
}

internal class AudioSettingDto
{
    public int SampleRate { get; set; } = 32000;
    public int Bitrate { get; set; } = 128000;
    public string Format { get; set; } = "mp3";
    public int Channel { get; set; } = 1;
}

internal class MiniMaxTtsResponse
{
    public MiniMaxBaseResp? BaseResp { get; set; }
    public MiniMaxAudioData? Data { get; set; }
}

internal class MiniMaxBaseResp
{
    public int StatusCode { get; set; }
    public string? StatusMsg { get; set; }
}

internal class MiniMaxAudioData
{
    public string? Audio { get; set; }
}

#endregion
