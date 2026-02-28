using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Services.Director;

/// <summary>
/// 故事板解析服务实现
/// 使用火山引擎 LLM 生成结构化剧本 JSON，然后映射为 Storyboard/Scene 供 Pipeline 下游使用
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

    public async Task<Storyboard> ParseAsync(string outputDirectory, string rawText, string? baseVisualStyle = null, CancellationToken ct = default)
    {
        var style  = baseVisualStyle ?? _pipelineOptions.DefaultVisualStyle;
        var charPrompt = _pipelineOptions.DefaultCharacterPrompt;

        // 解析首行内联配置，如 {{config:{strictlycnt:true, codesign:true}}}
        var (storyConfig, cleanedText) = ParseStoryConfig(rawText);
        var strictMode = storyConfig.StrictlyCnt;

        _logger.LogInformation("开始解析故事板，视觉风格: {Style}，严格原文模式: {StrictMode}", style, strictMode);

        try
        {
            List<Scene> scenes;
            ScreenplayJson? screenplayJson = null;

            if (strictMode)
            {
                var segments = await SplitIntoSegmentsWithLlmAsync(cleanedText, ct);
                (scenes, screenplayJson) = await ParseFullScreenplayAsync(cleanedText, style, charPrompt, segments, ct);
            }
            else
            {
                (scenes, screenplayJson) = await ParseFullScreenplayAsync(cleanedText, style, charPrompt, null, ct);
            }

            _logger.LogInformation("故事板解析完成，共 {Count} 个场景", scenes.Count);

            var storyboard = new Storyboard
            {
                BaseVisualStyle = style,
                Scenes = scenes
            };

            // 保存调试文件（raw JSON 和 Storyboard 两份）
            await SaveDebugFilesAsync(outputDirectory, storyboard, screenplayJson, ct);

            return storyboard;
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

    // ─── 默认模式：全量剧本生成（新格式）──────────────────────────────────

    private async Task<(List<Scene>, ScreenplayJson?)> ParseFullScreenplayAsync(
        string text, string style, string characterPrompt, List<string>? strictSegments, CancellationToken ct)
    {
        var systemPrompt = CreateFullScreenplayPrompt(style, characterPrompt, strictSegments);
        var responseText = await _llmService.GetResponseAsync(systemPrompt, text, ct);
        var jsonContent  = ExtractJsonFromResponse(responseText);

        ScreenplayJson? sp;
        try
        {
            sp = JsonSerializer.Deserialize<ScreenplayJson>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            _logger.LogWarning("新格式剧本解析失败，降级为旧格式解析");
            sp = null;
        }

        if (sp?.Shots == null || sp.Shots.Count == 0)
        {
            _logger.LogWarning("新格式未返回 shots，降级为旧格式解析");
            var scenes = await ParseLegacyAsync(text, style, characterPrompt, ct);
            return (scenes, null);
        }

        // 构建资产索引（用于解析 visualDescription 中的 [R1]/[L1]/[P1] 占位符）
        var charMap  = (sp.Assets?.Characters ?? []).ToDictionary(c => c.Id, c => c);
        var sceneMap = (sp.Assets?.Scenes ?? []).ToDictionary(s => s.Id, s => s);
        var propMap  = (sp.Assets?.Props ?? []).ToDictionary(p => p.Id, p => p);

        var result = sp.Shots.Select((shot, i) =>
        {
            // 解析 visualDescription 中的 [R1]/[L1]/[P1] 占位符，替换为实际描述
            var resolved = ResolveReferences(
                shot.VisualDescription ?? "",
                charMap, sceneMap, propMap);

            // 拼接 AIGC Prompt：全局风格 + 解析后的 visualDescription
            var visualPrompt = BuildVisualPrompt(style, characterPrompt, resolved);

            // 配音文本：优先使用 audio.text（旁白），其次用 audio.dialogue（角色台词）
            var speechText = shot.Audio?.Text
                ?? shot.Audio?.Dialogue
                ?? "";

            return new Scene
            {
                Index       = i + 1,
                SpeechText  = speechText,
                VisualPrompt = visualPrompt
            };
        }).ToList();

        return (result, sp);
    }

    /// <summary>
    /// 将 "[R1]"、"[L1]"、"[P1]" 占位符替换为对应资产的描述文字
    /// </summary>
    private static string ResolveReferences(
        string text,
        Dictionary<string, ScreenplayCharacter> charMap,
        Dictionary<string, ScreenplayScene>     sceneMap,
        Dictionary<string, ScreenplayProp>      propMap)
    {
        return Regex.Replace(text, @"\[([A-Z]\d+)\]", m =>
        {
            var id = m.Groups[1].Value;
            if (id.StartsWith('R') && charMap.TryGetValue(id, out var ch))
                return ch.Appearance ?? ch.Name;
            if (id.StartsWith('L') && sceneMap.TryGetValue(id, out var sc))
                return sc.Description ?? sc.Name;
            if (id.StartsWith('P') && propMap.TryGetValue(id, out var pr))
                return pr.Effect ?? pr.Name;
            return m.Value; // 未匹配保持原样
        });
    }

    // ─── 旧格式降级解析 ──────────────────────────────────────────────────

    private async Task<List<Scene>> ParseLegacyAsync(
        string text, string style, string characterPrompt, CancellationToken ct)
    {
        var systemPrompt = CreateLegacySystemPrompt(style, characterPrompt);
        var responseText  = await _llmService.GetResponseAsync(systemPrompt, text, ct);
        var jsonContent   = ExtractJsonFromResponse(responseText);

        var resp = JsonSerializer.Deserialize<StoryboardResponse>(
            jsonContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (resp?.Scenes == null || resp.Scenes.Count == 0)
            throw new InvalidOperationException("LLM 返回了空的场景列表");

        return resp.Scenes.Select((s, i) => new Scene
        {
            Index        = i + 1,
            SpeechText   = s.SpeechText,
            VisualPrompt = BuildVisualPrompt(style, characterPrompt, s.VisualPrompt)
        }).ToList();
    }



    // ─── 语义切分 ────────────────────────────────────────────────────────

    private async Task<List<string>> SplitIntoSegmentsWithLlmAsync(string text, CancellationToken ct)
    {
        try
        {
            var splitPrompt = CreateSemanticSplitPrompt();
            var responseText = await _llmService.GetResponseAsync(splitPrompt, text, ct);
            var jsonContent  = ExtractJsonFromResponse(responseText);

            var result = JsonSerializer.Deserialize<SemanticSplitResponse>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Segments != null && result.Segments.Count > 0)
            {
                var valid = result.Segments
                    .Where(s => !string.IsNullOrWhiteSpace(s) && text.Contains(s.Trim()))
                    .Select(s => s.Trim())
                    .ToList();

                if (valid.Count > 0)
                {
                    _logger.LogInformation("语义切分成功，共 {Count} 段", valid.Count);
                    return valid;
                }
            }
            _logger.LogWarning("LLM 语义切分结果无效，降级为标点切分");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 语义切分失败，降级为标点切分");
        }
        return SplitIntoSegments(text);
    }

    // ─── Prompts ─────────────────────────────────────────────────────────

    private static string CreateFullScreenplayPrompt(string style, string characterPrompt, List<string>? strictSegments)
    {
        var charHint = string.IsNullOrEmpty(characterPrompt)
            ? "（由 LLM 自行根据故事设定角色）"
            : $"用户已提供角色设定：{characterPrompt}，请将其纳入角色表并保持外观一致。";

        var strictRule = strictSegments != null && strictSegments.Count > 0
            ? $"\n\n## 严格原文匹配 (STRICT MODE)\n由于开启了严格模式，必须严格按照以下段落划分 shots。一共 {strictSegments.Count} 段，必须生成 {strictSegments.Count} 个 shot。每个 shot 的 audio 文本必须毫无修改、一字不差地使用该段落的精确字词！\n原文段落如下：\n" + string.Join("\n", strictSegments.Select((s, i) => $"Shot {i + 1}: {s}"))
            : "";

        return $"""
            你是一位专业的影视编剧和分镜师。请根据用户提供的故事内容，一次性生成完整结构化视频剧本。

            ## 全局配置
            - 视觉风格：{style}
            - 角色参考：{charHint}

            ## 编号规则
            - 角色：R0 = 旁白（无实体），R1 = 主角，R2/R3... = 配角
            - 场景：L1、L2...
            - 道具：P1、P2...
            - 分镜：S1、S2...

            ## visualDescription 写法
            每个镜头的 visualDescription 使用 [Rx]/[Lx]/[Px] 内联引用对应资产，例如：
            "远景平视。在 [L1] 中，[R1] 站立，[P2] 环绕飘动，[P1] 闪烁。镜头缓慢推近聚焦面部。"

            ## audio 字段规则
            - 旁白镜头：audio.voiceCharacterId = "R0"，audio.text = "旁白内容"
            - 角色台词：audio.voiceCharacterId = "R1"，audio.dialogue = "台词内容"
            - 纯画面镜头：省略 audio 字段

            ## 时长分配
            - 开篇/结尾镜头：3-5s；动作/高潮镜头：1-2s；普通叙事：2-3s
            - 有配音的镜头时长 ≥ 配音文字数 ÷ 5（中文约 5 字/秒）{strictRule}

            ## 输出格式
            仅以 JSON 格式返回，结构严格遵循：
            {FullScreenplayJsonTemplate}
            """;
    }

    private const string FullScreenplayJsonTemplate = """
        {
          "videoMetadata": {
            "title": "故事标题",
            "style": "视觉风格",
            "aspectRatio": "9:16",
            "totalDurationSeconds": 18.0,
            "packaging": {
              "themeColor": ["#E63946", "#F1FAEE"],
              "fontStyle": "字幕字体风格",
              "visualTone": "整体视觉基调"
            }
          },
          "assets": {
            "characters": [
              { "id": "R0", "name": "旁白", "voice": {"type": "Female", "description": "温和沉稳"} },
              { "id": "R1", "name": "主角名", "appearance": "外观描述", "voice": {"type": "Female", "description": "声音描述"} }
            ],
            "scenes": [
              { "id": "L1", "name": "场景名", "description": "场景视觉描述" }
            ],
            "props": [
              { "id": "P1", "name": "道具名", "effect": "道具视觉效果" }
            ]
          },
          "shots": [
            {
              "shotId": "S1",
              "duration": 3.0,
              "sceneId": "L1",
              "characterIds": ["R1"],
              "propIds": ["P1"],
              "audio": { "voiceCharacterId": "R0", "text": "旁白文本" },
              "visualDescription": "景别视角。在 [L1] 中，[R1] 动作，[P1] 视觉效果描述。"
            },
            {
              "shotId": "S2",
              "duration": 1.0,
              "characterIds": ["R1"],
              "audio": { "voiceCharacterId": "R1", "dialogue": "角色台词！" },
              "visualDescription": "特写平视。[R1] 嘴唇微张，表情激动。"
            }
          ]
        }
        """;

    private static string CreateLegacySystemPrompt(string style, string characterPrompt)
    {
        var charInstr = string.IsNullOrEmpty(characterPrompt)
            ? ""
            : $"\n主角/角色设定: {characterPrompt}\n";

        return $"""
            你是一个专业的视频脚本编剧。将用户提供的故事文本拆解为多个场景。
            全局视觉风格: {style}{charInstr}
            对于每个场景提供：
            1. speechText：台词或旁白（用于语音合成）
            2. visualPrompt：视觉描述（用于AI生图，具体、画面感强）
            仅以 JSON 格式返回：
            {"{"}"scenes": [{"{"}\"speechText\": \"...\", \"visualPrompt\": \"...\"{"}"}]{"}"}
            """;
    }



    private static string CreateSemanticSplitPrompt() => """
        你是一个专业的视频分镜助手。将用户提供的故事原文按情节/语义边界切分为若干段落，每段对应一个独立画面。
        切分规则：
        1. 每段必须是原文的完整、连续片段，不得修改任何文字
        2. 以情节转换、场景变化、人物动作转变为切分依据，同时尊重句末标点
        3. 每段建议 20-80 字，过短的句子与相邻句子合并
        仅以 JSON 格式返回：{"segments": ["原文片段1", "原文片段2", ...]}
        """;

    // ─── 工具函数 ─────────────────────────────────────────────────────────

    private static string BuildVisualPrompt(string style, string characterPrompt, string sceneDesc)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(style)) parts.Add(style);
        if (!string.IsNullOrEmpty(characterPrompt)) parts.Add(characterPrompt);
        if (!string.IsNullOrEmpty(sceneDesc)) parts.Add(sceneDesc);
        return string.Join(", ", parts);
    }

    private static string ExtractJsonFromResponse(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```json")) content = content[7..];
        else if (content.StartsWith("```")) content = content[3..];
        if (content.EndsWith("```")) content = content[..^3];
        return content.Trim();
    }

    private (StoryConfig config, string cleanedText) ParseStoryConfig(string rawText)
    {
        var lines     = rawText.Split('\n', 2, StringSplitOptions.None);
        var firstLine = lines[0].Trim();
        var match     = Regex.Match(firstLine, @"^\{\{(.+)\}\}$");
        if (!match.Success) return (new StoryConfig(), rawText);

        try
        {
            var inner       = match.Groups[1].Value.Trim();
            var configMatch = Regex.Match(inner, @"^config:\s*(\{.+\})$", RegexOptions.Singleline);
            var jsonPart    = configMatch.Success ? configMatch.Groups[1].Value : inner;

            var config = JsonSerializer.Deserialize<StoryConfig>(
                jsonPart,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new StoryConfig();

            var cleanedText = lines.Length > 1 ? lines[1].TrimStart('\r', '\n') : string.Empty;
            _logger.LogInformation("检测到内联配置: strictlycnt={StrictMode}, codesign={CoDesign}",
                config.StrictlyCnt, config.CoDesign);
            return (config, cleanedText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "首行配置解析失败，使用默认配置");
            return (new StoryConfig(), rawText);
        }
    }

    private static List<string> SplitIntoSegments(string text)
    {
        var rawSentences = Regex.Split(text.Trim(), @"(?<=[。！？!?…]+)")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var segments = new List<string>();
        var buffer   = string.Empty;
        foreach (var sentence in rawSentences)
        {
            buffer = string.IsNullOrEmpty(buffer) ? sentence : buffer + sentence;
            if (buffer.Length >= 20) { segments.Add(buffer); buffer = string.Empty; }
        }
        if (!string.IsNullOrEmpty(buffer)) segments.Add(buffer);
        return segments;
    }

    // ─── 调试文件保存 ─────────────────────────────────────────────────────

    private async Task SaveDebugFilesAsync(string outputDirectory, Storyboard storyboard, ScreenplayJson? screenplayJson, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var ts  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var opt = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // 1. 完整剧本 JSON（新格式）
            if (screenplayJson != null)
            {
                var spPath = Path.Combine(outputDirectory, $"screenplay_{ts}.json");
                await File.WriteAllTextAsync(spPath, JsonSerializer.Serialize(screenplayJson, opt), ct);
                _logger.LogInformation("剧本 JSON 已保存: {Path}", spPath);
            }
            else
            {
                // 如果是从旧格式降级过来的，还是存一下 storyboard 以便排查
                var sbPath = Path.Combine(outputDirectory, $"storyboard_{ts}.json");
                await File.WriteAllTextAsync(sbPath, JsonSerializer.Serialize(storyboard, opt), ct);
                _logger.LogInformation("降级分镜文件已保存: {Path}", sbPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调试文件写入失败（不影响主流程）");
        }
    }
}

// ─── 新格式 DTO ─────────────────────────────────────────────────────────────

internal class ScreenplayJson
{
    [JsonPropertyName("videoMetadata")]
    public VideoMetadataDto? VideoMetadata { get; set; }

    [JsonPropertyName("assets")]
    public AssetsDto? Assets { get; set; }

    [JsonPropertyName("shots")]
    public List<ShotDto>? Shots { get; set; }
}

internal class VideoMetadataDto
{
    public string? Title { get; set; }
    public string? Style { get; set; }
    public string? AspectRatio { get; set; }
    public double TotalDurationSeconds { get; set; }
    public PackagingDto? Packaging { get; set; }
}

internal class PackagingDto
{
    public List<string>? ThemeColor { get; set; }
    public string? FontStyle { get; set; }
    public string? VisualTone { get; set; }
}

internal class AssetsDto
{
    public List<ScreenplayCharacter>? Characters { get; set; }
    public List<ScreenplayScene>? Scenes { get; set; }
    public List<ScreenplayProp>? Props { get; set; }
}

internal class ScreenplayCharacter
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("appearance")]
    public string? Appearance { get; set; }
    [JsonPropertyName("voice")]
    public VoiceDto? Voice { get; set; }
}

internal class VoiceDto
{
    public string? Type { get; set; }
    public string? Description { get; set; }
}

internal class ScreenplayScene
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

internal class ScreenplayProp
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("effect")]
    public string? Effect { get; set; }
}

internal class ShotDto
{
    [JsonPropertyName("shotId")]
    public string? ShotId { get; set; }
    [JsonPropertyName("duration")]
    public double Duration { get; set; }
    [JsonPropertyName("sceneId")]
    public string? SceneId { get; set; }
    [JsonPropertyName("characterIds")]
    public List<string>? CharacterIds { get; set; }
    [JsonPropertyName("propIds")]
    public List<string>? PropIds { get; set; }
    [JsonPropertyName("audio")]
    public AudioDto? Audio { get; set; }
    [JsonPropertyName("visualDescription")]
    public string? VisualDescription { get; set; }
}

internal class AudioDto
{
    [JsonPropertyName("voiceCharacterId")]
    public string? VoiceCharacterId { get; set; }
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    [JsonPropertyName("dialogue")]
    public string? Dialogue { get; set; }
}

// ─── 旧格式 DTO（降级使用）─────────────────────────────────────────────────

internal class StoryboardResponse
{
    public List<SceneDto>? Scenes { get; set; }
}

internal class SceneDto
{
    public required string SpeechText { get; set; }
    public required string VisualPrompt { get; set; }
}

internal class VisualOnlyResponse
{
    public string? VisualPrompt { get; set; }
}

internal class SemanticSplitResponse
{
    public List<string>? Segments { get; set; }
}

// ─── 内联配置 DTO ─────────────────────────────────────────────────────────

internal class StoryConfig
{
    /// <summary>是否严格使用原文（不让 LLM 改写旁白）</summary>
    public bool StrictlyCnt { get; set; } = false;

    /// <summary>是否启用 CoDesign 共创模式（生成完成后暂停等待用户审阅）</summary>
    public bool CoDesign { get; set; } = false;
}
