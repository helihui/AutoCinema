using System.Text.Json;
using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Services.Director;

/// <summary>
/// 故事板解析服务实现
/// 使用火山引擎 LLM 实现结构化输出
/// </summary>
public class StoryboardService : IStoryboardService
{
    private readonly VolcengineLlmService _llmService;
    private readonly ILogger<StoryboardService> _logger;
    private readonly PipelineOptions _pipelineOptions;

    public StoryboardService(
        VolcengineLlmService llmService,
        ILogger<StoryboardService> logger,
        IOptions<PipelineOptions> pipelineOptions)
    {
        _llmService = llmService;
        _logger = logger;
        _pipelineOptions = pipelineOptions.Value;
    }

    public async Task<Storyboard> ParseAsync(string rawText, string? baseVisualStyle = null, CancellationToken ct = default)
    {
        var style = baseVisualStyle ?? _pipelineOptions.DefaultVisualStyle;
        var characterPrompt = _pipelineOptions.DefaultCharacterPrompt;

        _logger.LogInformation("开始解析故事板，视觉风格: {Style}", style);
        if (!string.IsNullOrEmpty(characterPrompt))
        {
            _logger.LogInformation("角色设定: {Character}", characterPrompt);
        }

        var systemPrompt = CreateSystemPrompt(style, characterPrompt);

        try
        {
            // 使用火山引擎 LLM 获取响应
            var responseText = await _llmService.GetResponseAsync(systemPrompt, rawText, ct);

            // 从响应中提取 JSON 内容
            var jsonContent = ExtractJsonFromResponse(responseText);

            // 解析 JSON
            var storyboardResponse = JsonSerializer.Deserialize<StoryboardResponse>(
                jsonContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (storyboardResponse?.Scenes == null || storyboardResponse.Scenes.Count == 0)
            {
                throw new InvalidOperationException("LLM 返回了空的场景列表");
            }

            // 为每个场景添加索引
            var scenes = new List<Scene>();
            for (int i = 0; i < storyboardResponse.Scenes.Count; i++)
            {
                var sceneDto = storyboardResponse.Scenes[i];

                // 构建完整的 VisualPrompt
                // 格式: [全局风格], [角色设定], [场景描述]
                var fullVisualPrompt = style;
                if (!string.IsNullOrEmpty(characterPrompt))
                {
                    fullVisualPrompt += $", {characterPrompt}";
                }
                fullVisualPrompt += $", {sceneDto.VisualPrompt}";

                scenes.Add(new Scene
                {
                    Index = i + 1,
                    SpeechText = sceneDto.SpeechText,
                    VisualPrompt = fullVisualPrompt
                });
            }

            _logger.LogInformation("故事板解析完成，共 {Count} 个场景", scenes.Count);

            return new Storyboard
            {
                BaseVisualStyle = style,
                Scenes = scenes
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON 解析失败");
            throw new InvalidOperationException("LLM 返回的内容无法解析为有效的 JSON", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "故事板解析失败");
            throw;
        }
    }

    /// <summary>
    /// 从响应中提取 JSON 内容（处理 markdown 代码块）
    /// </summary>
    private static string ExtractJsonFromResponse(string content)
    {
        content = content.Trim();

        // 如果内容被 markdown 代码块包裹
        if (content.StartsWith("```json"))
        {
            content = content[7..]; // 移除 ```json
        }
        else if (content.StartsWith("```"))
        {
            content = content[3..]; // 移除 ```
        }

        if (content.EndsWith("```"))
        {
            content = content[..^3]; // 移除结尾的 ```
        }

        return content.Trim();
    }

    private static string CreateSystemPrompt(string style, string characterPrompt)
    {
        var characterInstruction = string.IsNullOrEmpty(characterPrompt)
            ? ""
            : $"\n            主角/角色设定: {characterPrompt}\n";

        return $"""
            你是一个专业的视频脚本编剧。你的任务是将用户提供的故事文本拆解为多个场景。

            全局视觉风格: {style}{characterInstruction}

            对于每个场景，你需要提供：
            1. SpeechText: 该场景的台词或旁白文本（用于语音合成，保持自然流畅）
            2. VisualPrompt: 该场景的视觉描述（用于AI生图），应该具体、画面感强

            重要规则：
            - 保持角色外貌描述的一致性（每次提及同一角色时使用相同的外貌描述）
            - 如果提供了主角/角色设定，请确保在生成 VisualPrompt 时，如果场景涉及该角色，必须包含其核心特征，但不要直接照抄整个设定字符串（系统会自动拼接），而是要根据场景动作自然地描述。
            - 每个场景的台词适中，不要太长（建议每段30-100字）
            - 视觉描述要详细具体，包含环境、人物动作、光线氛围等
            - 场景之间要有逻辑连贯性

            你必须仅以 JSON 格式返回（不要添加任何其他文字），结构如下：
            """ + """
            {
                "scenes": [
                    {
                        "speechText": "台词内容",
                        "visualPrompt": "视觉描述（不需要包含全局风格和固定的角色设定，系统会自动添加）"
                    }
                ]
            }
            """;
    }
}

/// <summary>
/// 用于 LLM 结构化输出的响应 DTO
/// </summary>
internal class StoryboardResponse
{
    public List<SceneDto>? Scenes { get; set; }
}

internal class SceneDto
{
    public required string SpeechText { get; set; }
    public required string VisualPrompt { get; set; }
}
