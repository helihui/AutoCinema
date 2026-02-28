using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Audio;
using AutoCinema.Pro.Services.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Services.Actor;

/// <summary>
/// MiniMax 声音配置服务
/// </summary>
public class MiniMaxVoiceConfigurationService : IVoiceConfigurationService
{
    private readonly HttpClient _httpClient;
    private readonly MiniMaxOptions _options;
    private readonly ILogger<MiniMaxVoiceConfigurationService> _logger;
    private readonly ISpeechGenerationService _speechService;
    private readonly IAudioPreprocessorService _audioPreprocessor;

    public MiniMaxVoiceConfigurationService(
        HttpClient httpClient,
        IOptions<MiniMaxOptions> options,
        ILogger<MiniMaxVoiceConfigurationService> logger,
        ISpeechGenerationService speechService,
        IAudioPreprocessorService audioPreprocessor)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _speechService = speechService;
        _audioPreprocessor = audioPreprocessor;
    }

    /// <summary>
    /// MiniMax 支持声音克隆
    /// </summary>
    public bool SupportsVoiceCloning => true;

    public async Task<List<VoiceProfile>> GetAvailableVoicesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("正在获取可用声音列表...");

            var requestBody = new GetVoiceRequest
            {
                VoiceType = "all" // 获取所有类型: system, voice_cloning, voice_generation
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minimaxi.com/v1/get_voice")
            {
                Content = JsonContent.Create(requestBody, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            GetVoiceResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<GetVoiceResponse>(
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
                    ct);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning(jsonEx, "MiniMax 声音列表 JSON 解析失败，使用本地备用列表");
                return GetFallbackVoices();
            }


            if (result?.BaseResp?.StatusCode != 0)
            {
                _logger.LogWarning(
                    "MiniMax 获取声音列表失败 (代码: {Code}): {Msg}。使用本地备用列表。",
                    result?.BaseResp?.StatusCode,
                    result?.BaseResp?.StatusMsg);
                return GetFallbackVoices();
            }

            var voices = new List<VoiceProfile>();

            // 转换系统预设声音
            if (result?.Voices != null)
            {
                foreach (var voice in result.Voices)
                {
                    voices.Add(new VoiceProfile
                    {
                        VoiceId = voice.VoiceId,
                        DisplayName = voice.Name ?? voice.VoiceId,
                        Description = voice.Description != null && voice.Description.Count > 0
                            ? string.Join(", ", voice.Description)
                            : null,
                        Language = voice.Language ?? "zh-CN",
                        Gender = ParseGender(voice.Gender),
                        Tags = voice.Tags ?? new List<string>(),
                        IsCloned = false,
                        PreviewUrl = voice.PreviewUrl
                    });
                }
            }

            // 转换克隆声音
            if (result?.VoiceCloning != null)
            {
                foreach (var voice in result.VoiceCloning)
                {
                    voices.Add(new VoiceProfile
                    {
                        VoiceId = voice.VoiceId,
                        DisplayName = voice.Name ?? $"克隆声音_{voice.VoiceId}",
                        Description = "用户克隆的声音",
                        Language = "zh-CN",
                        Gender = VoiceGender.Neutral,
                        IsCloned = true,
                        CreatedAt = voice.CreatedAt.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(voice.CreatedAt.Value).DateTime
                            : DateTime.UtcNow
                    });
                }
            }

            _logger.LogInformation("成功获取 {Count} 个声音", voices.Count);
            return voices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取声音列表失败,使用备用列表");
            return GetFallbackVoices();
        }
    }

    public async Task<VoiceProfile?> GetVoiceDetailsAsync(string voiceId, CancellationToken ct = default)
    {
        var voices = await GetAvailableVoicesAsync(ct);
        return voices.FirstOrDefault(v => v.VoiceId == voiceId);
    }

    public async Task<string> PreviewVoiceAsync(string voiceId, string sampleText, CancellationToken ct = default)
    {
        _logger.LogInformation("生成声音预览: {VoiceId}", voiceId);

        // 使用固定路径作为缓存（按 voiceId 区分），避免重复调用付费 API
        var cacheDir = Path.Combine(Path.GetTempPath(), "autocinema_voice_cache");
        Directory.CreateDirectory(cacheDir);
        var cachedPath = Path.Combine(cacheDir, $"preview_{voiceId}.mp3");

        if (File.Exists(cachedPath))
        {
            _logger.LogInformation("使用缓存的声音预览: {Path}", cachedPath);
            return cachedPath;
        }

        _logger.LogInformation("缓存未命中，调用 API 生成预览...");
        await _speechService.GenerateAsync(sampleText, cachedPath, new VoiceGenerationConfig { VoiceId = voiceId }, ct);
        _logger.LogInformation("声音预览已缓存: {Path}", cachedPath);
        return cachedPath;
    }

    public async Task<VoiceProfile> CloneVoiceAsync(string name, string audioFilePath, CancellationToken ct = default)
    {
        _logger.LogInformation("开始克隆声音: {Name}", name);

        try
        {
            // 步骤零：音频预处理（降噪、去静音、格式归一化）
            _logger.LogInformation("正在对音频进行预处理...");
            var requirements = VoiceCloneRequirements.MiniMax;
            var preprocessResult = await _audioPreprocessor.PrepareVoiceCloneAudioAsync(audioFilePath, requirements, ct);
            _logger.LogInformation("音频预处理完成: 时长={Duration}s, 大小={Size}KB",
                preprocessResult.ActualDuration.TotalSeconds, preprocessResult.FileSizeBytes / 1024);

            var processedFilePath = preprocessResult.ProcessedFilePath;

            // 步骤一：上传预处理后的音频文件获取 file_id
            var fileId = await UploadFileAsync(processedFilePath, "voice_clone", ct);
            if (string.IsNullOrEmpty(fileId))
            {
                throw new InvalidOperationException("无法获取上传文件的 file_id");
            }

            _logger.LogInformation("文件上传成功, FileId: {FileId}", fileId);

            // 步骤二：调用 voice_clone 接口
            // 注意：API 要求 voice_id 支持大小写字母、数字，不能包含其他字符(包括下划线)，并且必须以小写字母开头，长度在 8-64 字符
            var voiceIdSafe = $"clone{Guid.NewGuid():N}"; 

            if (!long.TryParse(fileId, out var fileIdLong))
            {
                throw new InvalidOperationException($"API 返回的 file_id 格式不正确，期望为纯数字: {fileId}");
            }

            var requestBody = new 
            {
                file_id = fileIdLong,
                voice_id = voiceIdSafe
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minimaxi.com/v1/voice_clone")
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, ct);
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("克隆声音接口响应: {Response}", responseJson);

            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<VoiceCloneResponse>(
                responseJson,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (result?.BaseResp?.StatusCode != 0)
            {
                throw new InvalidOperationException($"声音克隆失败: {result?.BaseResp?.StatusMsg}");
            }

            var actualVoiceId = result?.Data?.VoiceId ?? voiceIdSafe; // fallback to our generated if API doesn't return it
            _logger.LogInformation("声音克隆成功: {VoiceId}", actualVoiceId);

            return new VoiceProfile
            {
                VoiceId = actualVoiceId,
                DisplayName = name,
                Description = "用户克隆的声音",
                Language = "zh-CN",
                Gender = VoiceGender.Neutral,
                IsCloned = true,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "声音克隆失败");
            throw;
        }
    }

    /// <summary>
    /// 上传文件到 MiniMax
    /// </summary>
    private async Task<string?> UploadFileAsync(string filePath, string purpose, CancellationToken ct)
    {
        var audioBytes = await File.ReadAllBytesAsync(filePath, ct);
        var audioContent = new ByteArrayContent(audioBytes);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var mimeType = extension switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

        var formData = new MultipartFormDataContent
        {
            { new StringContent(purpose), "purpose" },
            { audioContent, "file", Path.GetFileName(filePath) }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minimaxi.com/v1/files/upload")
        {
            Content = formData
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var response = await _httpClient.SendAsync(request, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.TryGetProperty("file", out var fileElement) && 
            fileElement.TryGetProperty("file_id", out var fileIdElement))
        {
             return fileIdElement.ToString();
        }

        return null;
    }

    /// <summary>
    /// 备用声音列表(当 API 调用失败时使用)
    /// </summary>
    private List<VoiceProfile> GetFallbackVoices()
    {
        return new List<VoiceProfile>
        {
            new() { VoiceId = "male-qn-qingse", DisplayName = "青涩青年(男)", Gender = VoiceGender.Male, Tags = new() { "年轻", "清晰" } },
            new() { VoiceId = "female-shaonv", DisplayName = "少女音", Gender = VoiceGender.Female, Tags = new() { "甜美", "活泼" } },
            new() { VoiceId = "male-qn-jingying", DisplayName = "精英男声", Gender = VoiceGender.Male, Tags = new() { "成熟", "专业" } },
            new() { VoiceId = "female-yujie", DisplayName = "御姐音", Gender = VoiceGender.Female, Tags = new() { "成熟", "磁性" } },
            new() { VoiceId = "presenter_male", DisplayName = "男性主持人", Gender = VoiceGender.Male, Tags = new() { "播音", "标准" } },
            new() { VoiceId = "presenter_female", DisplayName = "女性主持人", Gender = VoiceGender.Female, Tags = new() { "播音", "标准" } },
            new() { VoiceId = "audiobook_male_1", DisplayName = "有声书男声1", Gender = VoiceGender.Male, Tags = new() { "沉稳", "叙事" } },
            new() { VoiceId = "audiobook_female_1", DisplayName = "有声书女声1", Gender = VoiceGender.Female, Tags = new() { "温柔", "叙事" } },
        };
    }

    private static VoiceGender ParseGender(string? gender)
    {
        return gender?.ToLower() switch
        {
            "male" or "男" => VoiceGender.Male,
            "female" or "女" => VoiceGender.Female,
            _ => VoiceGender.Neutral
        };
    }
}

#region API Models

internal class GetVoiceRequest
{
    [JsonPropertyName("voice_type")]
    public required string VoiceType { get; set; } // "system", "voice_cloning", "all"
}

internal class GetVoiceResponse
{
    [JsonPropertyName("base_resp")]
    public MiniMaxBaseResp? BaseResp { get; set; }

    [JsonPropertyName("system_voice")]
    public List<SystemVoice>? Voices { get; set; }

    [JsonPropertyName("voice_cloning")]
    public List<ClonedVoice>? VoiceCloning { get; set; }
}

// 移除不再需要的 VoiceData 类

internal class SystemVoice
{
    [JsonPropertyName("voice_id")]
    public required string VoiceId { get; set; }

    [JsonPropertyName("voice_name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public List<string>? Description { get; set; } // API 返回的是字符串数组

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("preview_url")]
    public string? PreviewUrl { get; set; }
}

internal class ClonedVoice
{
    [JsonPropertyName("voice_id")]
    public required string VoiceId { get; set; }

    [JsonPropertyName("voice_name")]
    public string? Name { get; set; }

    [JsonPropertyName("created_time")]
    public long? CreatedAt { get; set; } // API 实际返回的是 created_time
}

internal class VoiceCloneResponse
{
    [JsonPropertyName("base_resp")]
    public MiniMaxBaseResp? BaseResp { get; set; }
    
    [JsonPropertyName("data")]
    public VoiceCloneData? Data { get; set; }
}

internal class VoiceCloneData
{
    [JsonPropertyName("voice_id")]
    public required string VoiceId { get; set; }
}

#endregion


