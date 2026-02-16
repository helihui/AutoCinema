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
/// 火山引擎 Seedream 图片生成服务
/// </summary>
public class VolcengineImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly VolcengineOptions _options;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<VolcengineImageService> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public VolcengineImageService(
        HttpClient httpClient,
        IOptions<VolcengineOptions> options,
        ILogger<VolcengineImageService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_options.MaxConcurrency);
        _retryPolicy = PollyPolicies.GetImageGenerationPolicy();
    }

    public async Task<string> GenerateAsync(string prompt, string outputPath, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            _logger.LogDebug("开始生成图片: {Prompt}", prompt[..Math.Min(50, prompt.Length)] + "...");

            var requestBody = new VolcengineImageRequest
            {
                Model = _options.Model,
                Prompt = prompt,
                SequentialImageGeneration = _options.SequentialImageGeneration,
                ResponseFormat = _options.ResponseFormat,
                Size = _options.ImageSize,
                Stream = _options.Stream,
                Watermark = _options.Watermark
            };

            // 只有在配置了 Seed 时才添加
            if (_options.Seed.HasValue)
            {
                requestBody.Seed = _options.Seed.Value;
            }

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

            var result = await response.Content.ReadFromJsonAsync<VolcengineImageResponse>(
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
                ct);

            if (result?.Data == null || result.Data.Count == 0)
            {
                throw new InvalidOperationException("火山引擎返回了空的图片数据");
            }

            // 确保目录存在
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 根据响应格式处理图片数据
            if (_options.ResponseFormat == "url")
            {
                // URL 格式：下载图片
                var imageUrl = result.Data[0].Url;
                if (string.IsNullOrEmpty(imageUrl))
                {
                    throw new InvalidOperationException("火山引擎返回的 URL 为空");
                }

                _logger.LogDebug("下载图片: {Url}", imageUrl);
                var imageData = await _httpClient.GetByteArrayAsync(imageUrl, ct);
                await File.WriteAllBytesAsync(outputPath, imageData, ct);
            }
            else
            {
                // b64_json 格式：解码 Base64
                var b64Json = result.Data[0].B64Json;
                if (string.IsNullOrEmpty(b64Json))
                {
                    throw new InvalidOperationException("火山引擎返回的 b64_json 为空");
                }

                var imageData = Convert.FromBase64String(b64Json);
                await File.WriteAllBytesAsync(outputPath, imageData, ct);
            }

            _logger.LogInformation("图片生成成功: {Path}", outputPath);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "图片生成失败，使用降级方案: {Prompt}", prompt[..Math.Min(30, prompt.Length)]);
            return await CreateFallbackImageAsync(outputPath, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> CreateFallbackImageAsync(string outputPath, CancellationToken ct)
    {
        // 降级策略：创建一个简单的占位图（纯灰色 PNG）
        var minimalGrayPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 pixel
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0x78, 0x78, 0x78, 0x00,
            0x00, 0x00, 0x04, 0x00, 0x01, 0x27, 0x34, 0x5D,
            0xCE, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(outputPath, minimalGrayPng, ct);
        _logger.LogWarning("使用降级占位图: {Path}", outputPath);
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

internal class VolcengineImageRequest
{
    public required string Model { get; set; }
    public required string Prompt { get; set; }
    public int? Seed { get; set; }
    public required string SequentialImageGeneration { get; set; }
    public required string ResponseFormat { get; set; }
    public required string Size { get; set; }
    public required bool Stream { get; set; }
    public required bool Watermark { get; set; }
}

internal class VolcengineImageResponse
{
    public List<VolcengineImageData>? Data { get; set; }
}

internal class VolcengineImageData
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

#endregion
