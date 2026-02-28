using System.Text.Json;
using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models.Screenplay;
using AutoCinema.Pro.Services.Director;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Services.Screenplay;

/// <summary>
/// 全量剧本生成服务（AC-Node: StoryGenerator + ShotPlanner + PromptBuilder）
///
/// 一次 LLM 调用，产出完整结构化剧本（含分镜列表）。
/// 镜头数量由 LLM 根据故事内容和节奏自主决定，不由用户预设。
///
/// 🔑 设计要点：分镜通过 ID（R1/L1/P1）引用角色/场景/道具，
///    AIGC Prompt 构建时自动查表注入对应元素的描述，保证 Prompt 完整性和一致性。
/// </summary>
public class ScreenplayGeneratorService
{
    private readonly VolcengineLlmService _llmService;
    private readonly ILogger<ScreenplayGeneratorService> _logger;
    private readonly PipelineOptions _pipelineOptions;

    public ScreenplayGeneratorService(
        VolcengineLlmService llmService,
        ILogger<ScreenplayGeneratorService> logger,
        IOptions<PipelineOptions> pipelineOptions)
    {
        _llmService = llmService;
        _logger = logger;
        _pipelineOptions = pipelineOptions.Value;
    }

    /// <summary>
    /// 根据用户输入（故事文本 + 项目配置 + 角色/场景/道具设定）生成完整剧本。
    /// </summary>
    public async Task<ScreenplayDocument> GenerateAsync(
        string storyIntent,
        ProjectConfig config,
        List<CharacterAnchor> characterAnchors,
        List<SceneSetup> sceneSetups,
        List<PropSetup> propSetups,
        CancellationToken ct = default)
    {
        _logger.LogInformation("开始生成完整剧本，总时长={Duration}s，最大镜头={MaxShot}",
            config.TotalDurationSeconds, config.MaxShotCount?.ToString() ?? "由内容决定");

        var systemPrompt = BuildSystemPrompt(config, characterAnchors, sceneSetups, propSetups);
        var responseText = await _llmService.GetResponseAsync(systemPrompt, storyIntent, ct);
        var jsonContent = ExtractJson(responseText);

        ScreenplayDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ScreenplayDto>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "剧本 JSON 解析失败，原始响应长度: {Len}", responseText.Length);
            throw new InvalidOperationException("LLM 返回的剧本内容无法解析为有效 JSON", ex);
        }

        if (dto?.Shots == null || dto.Shots.Count == 0)
            throw new InvalidOperationException("LLM 生成的剧本不包含任何分镜");

        var screenplay = MapFromDto(dto, config, characterAnchors);

        _logger.LogInformation("剧本生成完成：标题={Title}，共 {ShotCount} 个分镜，预计时长={Duration}s",
            screenplay.Title, screenplay.Shots.Count,
            screenplay.Shots.Sum(s => s.DurationSeconds));

        // 将剧本写入文件，方便调试
        await SaveDebugFilesAsync(responseText, screenplay, ct);

        return screenplay;
    }

    /// <summary>
    /// 将 LLM 原始响应和解析后的剧本保存为文件，方便调试。
    /// 文件写入失败不影响主流程（仅打 warning 日志）。
    /// </summary>
    private async Task SaveDebugFilesAsync(
        string rawResponse,
        ScreenplayDocument screenplay,
        CancellationToken ct)
    {
        try
        {
            // 固定写到应用目录下的 output/screenplays，无需额外配置
            var dir = Path.Combine(AppContext.BaseDirectory, "output", "screenplays");
            Directory.CreateDirectory(dir);

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // 1. LLM 原始响应（调试时可看到 LLM 到底返回了什么）
            var rawPath = Path.Combine(dir, $"screenplay_raw_{ts}.txt");
            await File.WriteAllTextAsync(rawPath, rawResponse, ct);

            // 2. 解析后的剧本 JSON（格式化，人类可读）
            var jsonPath = Path.Combine(dir, $"screenplay_{ts}.json");
            var json = JsonSerializer.Serialize(screenplay, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            await File.WriteAllTextAsync(jsonPath, json, ct);

            _logger.LogInformation("剧本已保存：\n  JSON → {JsonPath}\n  RAW  → {RawPath}", jsonPath, rawPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "剧本调试文件写入失败（不影响主流程）");
        }
    }

    private ScreenplayDocument MapFromDto(ScreenplayDto dto, ProjectConfig config, List<CharacterAnchor> anchors)
    {
        // 先构建角色/场景/道具查找表（用于 AIGC Prompt 注入）
        var characters = dto.Characters?.Select(c => new CharacterSetup
        {
            Id = c.Id ?? "",
            Name = c.Name ?? "",
            Appearance = c.Appearance ?? "",
            Role = c.Role ?? "",
            Personality = c.Personality ?? "",
            VoiceDescription = c.VoiceDescription ?? ""
        }).ToList() ?? [];

        var scenes = dto.Scenes?.Select(s => new SceneSetup
        {
            Id = s.Id ?? "",
            Name = s.Name ?? "",
            Atmosphere = s.Atmosphere ?? "",
            TimeOfDay = s.TimeOfDay ?? "",
            VisualDescription = s.VisualDescription ?? ""
        }).ToList() ?? [];

        var props = dto.Props?.Select(p => new PropSetup
        {
            Id = p.Id ?? "",
            Name = p.Name ?? "",
            Description = p.Description ?? "",
            VisualEffect = p.VisualEffect ?? ""
        }).ToList() ?? [];

        // 以 ID 为 key 建索引，供分镜引用查找
        var characterById = characters.Where(c => !string.IsNullOrEmpty(c.Id))
                                      .ToDictionary(c => c.Id, c => c);
        var sceneById     = scenes.Where(s => !string.IsNullOrEmpty(s.Id))
                                  .ToDictionary(s => s.Id, s => s);
        var propById      = props.Where(p => !string.IsNullOrEmpty(p.Id))
                                 .ToDictionary(p => p.Id, p => p);

        // 映射分镜，AIGC Prompt 通过引用 ID 自动注入元素描述
        var shots = dto.Shots!.Select((s, i) => new Shot
        {
            Index             = i + 1,
            DurationSeconds   = s.DurationSeconds > 0 ? s.DurationSeconds : 5,
            FrameType         = s.FrameType ?? "中景",
            CameraAngle       = s.CameraAngle ?? "平视",
            Action            = s.Action ?? "",
            Transition        = s.Transition ?? "切换",
            NarrationText     = s.NarrationText ?? "",
            NarrationOwner    = s.NarrationOwner ?? "旁白",
            VoiceCharacterId  = s.VoiceCharacterId ?? "R0",
            CharacterDialogue = s.CharacterDialogue,
            SceneId           = s.SceneId,
            SceneName         = s.SceneId != null && sceneById.TryGetValue(s.SceneId, out var scene)
                                    ? scene.Name : s.SceneName,
            CharacterIds      = s.CharacterIds ?? [],
            PropIds           = s.PropIds ?? [],
            // AIGC Prompt：通过引用 ID 查表自动拼入角色/场景/道具描述
            AigcPrompt = BuildAigcPromptWithRefs(
                rawPrompt:    s.AigcPrompt ?? s.Action ?? "",
                config:       config,
                anchors:      anchors,
                characterIds: s.CharacterIds ?? [],
                sceneId:      s.SceneId,
                propIds:      s.PropIds ?? [],
                characterById: characterById,
                sceneById:     sceneById,
                propById:      propById)
        }).ToList();

        // 时长兜底：若总时长超出，等比压缩
        int totalDuration = shots.Sum(s => s.DurationSeconds);
        int maxAllowed    = (int)(config.TotalDurationSeconds * 0.9);
        if (totalDuration > maxAllowed && totalDuration > 0)
        {
            double ratio = (double)maxAllowed / totalDuration;
            _logger.LogInformation("时长超出限制（{Total}s > {Max}s），等比压缩，比例={Ratio:P0}", totalDuration, maxAllowed, ratio);
            shots = shots.Select(s => s with { DurationSeconds = Math.Max(3, (int)(s.DurationSeconds * ratio)) }).ToList();
        }

        // 镜头数量兜底：超出 MaxShotCount 时合并相邻短镜头
        if (config.MaxShotCount.HasValue && shots.Count > config.MaxShotCount.Value)
        {
            _logger.LogInformation("镜头数超出软上限（{Count} > {Max}），合并短镜头", shots.Count, config.MaxShotCount.Value);
            shots = MergeShortShots(shots, config.MaxShotCount.Value);
        }

        return new ScreenplayDocument
        {
            Title        = dto.Title ?? "未命名剧本",
            Requirements = dto.Requirements ?? "",
            Synopsis     = dto.Synopsis ?? "",
            WrappingStyle = dto.WrappingStyle ?? config.StyleTheme,
            Characters   = characters,
            Scenes       = scenes,
            Props        = props,
            Shots        = shots,
            Notes        = dto.Notes ?? ""
        };
    }

    /// <summary>
    /// 构建 AIGC Prompt：
    /// 1. 注入全局风格/色调
    /// 2. 通过 CharacterIds 查表注入角色外观描述
    /// 3. 通过 SceneId 查表注入场景视觉描述
    /// 4. 通过 PropIds 查表注入道具视觉效果描述
    /// 5. 追加 LLM 生成的原始画面描述
    /// </summary>
    private static string BuildAigcPromptWithRefs(
        string rawPrompt,
        ProjectConfig config,
        List<CharacterAnchor> anchors,
        List<string> characterIds,
        string? sceneId,
        List<string> propIds,
        Dictionary<string, CharacterSetup> characterById,
        Dictionary<string, SceneSetup> sceneById,
        Dictionary<string, PropSetup> propById)
    {
        var parts = new List<string>();

        // 1. 全局风格和色调
        if (!string.IsNullOrEmpty(config.StyleTheme)) parts.Add(config.StyleTheme);
        if (!string.IsNullOrEmpty(config.ColorTone))  parts.Add(config.ColorTone);

        // 2. 角色外观描述（CharacterIds 引用）
        foreach (var id in characterIds)
        {
            if (characterById.TryGetValue(id, out var ch) && !string.IsNullOrEmpty(ch.Appearance))
                parts.Add(ch.Appearance);
        }

        // 3. CharacterAnchor（用户上传的角色图关键词，优先级高于 LLM 生成的角色设定）
        foreach (var anchor in anchors)
        {
            var snippet = anchor.ToPromptSnippet();
            if (!string.IsNullOrEmpty(snippet)) parts.Add(snippet);
        }

        // 4. 场景视觉描述（SceneId 引用）
        if (sceneId != null && sceneById.TryGetValue(sceneId, out var scene))
        {
            if (!string.IsNullOrEmpty(scene.VisualDescription)) parts.Add(scene.VisualDescription);
            else if (!string.IsNullOrEmpty(scene.Atmosphere))   parts.Add(scene.Atmosphere);
        }

        // 5. 道具视觉效果（PropIds 引用）
        foreach (var id in propIds)
        {
            if (propById.TryGetValue(id, out var prop) && !string.IsNullOrEmpty(prop.VisualEffect))
                parts.Add(prop.VisualEffect);
        }

        // 6. LLM 生成的原始画面描述（景别/视角/动作）
        if (!string.IsNullOrEmpty(rawPrompt)) parts.Add(rawPrompt);

        return string.Join(", ", parts);
    }

    private static List<Shot> MergeShortShots(List<Shot> shots, int maxCount)
    {
        while (shots.Count > maxCount)
        {
            int minIdx = 0;
            for (int i = 1; i < shots.Count - 1; i++)
            {
                if (shots[i].DurationSeconds + shots[i + 1].DurationSeconds <
                    shots[minIdx].DurationSeconds + shots[minIdx + 1].DurationSeconds)
                    minIdx = i;
            }
            var merged = shots[minIdx] with
            {
                DurationSeconds = shots[minIdx].DurationSeconds + shots[minIdx + 1].DurationSeconds,
                NarrationText   = shots[minIdx].NarrationText + " " + shots[minIdx + 1].NarrationText,
                Action          = shots[minIdx].Action + "；" + shots[minIdx + 1].Action,
                // 合并后取并集的角色和道具引用
                CharacterIds = shots[minIdx].CharacterIds.Union(shots[minIdx + 1].CharacterIds).ToList(),
                PropIds      = shots[minIdx].PropIds.Union(shots[minIdx + 1].PropIds).ToList()
            };
            shots.RemoveAt(minIdx + 1);
            shots[minIdx] = merged;
            shots = shots.Select((s, i) => s with { Index = i + 1 }).ToList();
        }
        return shots;
    }

    private static string ExtractJson(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```json")) content = content[7..];
        else if (content.StartsWith("```")) content = content[3..];
        if (content.EndsWith("```")) content = content[..^3];
        return content.Trim();
    }

    private static string BuildSystemPrompt(
        ProjectConfig config,
        List<CharacterAnchor> anchors,
        List<SceneSetup> scenes,
        List<PropSetup> props)
    {
        var anchorsDesc = anchors.Count > 0
            ? string.Join("\n", anchors.Select(a =>
                $"- {a.CharacterName}：{a.ToPromptSnippet()}" +
                (a.ForbiddenWords.Length > 0 ? $"（禁用词：{string.Join(",", a.ForbiddenWords)}）" : "")))
            : "（由 LLM 在剧本中自行设定）";

        var scenesHint = scenes.Count > 0
            ? string.Join("\n", scenes.Select((s, i) => $"- L{i + 1} {s.Name}：{s.Atmosphere}"))
            : "（由 LLM 在剧本中自行设定）";

        var propsHint = props.Count > 0
            ? string.Join("\n", props.Select((p, i) => $"- P{i + 1} {p.Name}：{p.VisualEffect}"))
            : "（由 LLM 在剧本中自行设定）";

        var maxShotNote = config.MaxShotCount.HasValue
            ? $"镜头总数不超过 {config.MaxShotCount.Value} 个。"
            : "镜头数量由故事内容自然决定，不设固定上限。";

        return $"""
            你是一位专业的影视编剧和分镜师。请根据用户提供的故事意图，一次性产出一份完整的视频剧本。

            ## 全局配置
            - 画幅：{config.AspectRatio}
            - 目标总时长：{config.TotalDurationSeconds} 秒（所有镜头时长之和不超过 {(int)(config.TotalDurationSeconds * 0.9)} 秒）
            - 视觉风格：{config.StyleTheme}
            - 色调：{config.ColorTone}
            - {maxShotNote}

            ## 用户提供的角色参考（必须保持外观一致）
            {anchorsDesc}

            ## 用户提供的场景参考（可在此基础上扩展）
            {scenesHint}

            ## 用户提供的道具参考（可在此基础上扩展）
            {propsHint}

            ## 编号规则（务必遵守）
            - 角色编号：R0 = 旁白（无实体），R1 = 主角，R2/R3... = 配角
            - 场景编号：L1、L2...
            - 道具编号：P1、P2...
            - 每个 `shots` 元素必须包含：
              - `characterIds`：本镜头出现的角色 ID 数组，如 ["R1"]（旁白独白不出现画面则留 []）
              - `sceneId`：本镜头所在场景编号，如 "L1"
              - `propIds`：本镜头涉及的道具 ID 数组，如 ["P1","P2"]
              - `narrationOwner`：配音角色名，如 "旁白" 或 "红衣女子"
              - `voiceCharacterId`：对应角色编号，如 "R0"（旁白）、"R1"（主角）

            ## 分镜规划原则
            - 根据情节的起承转合自然划分镜头，不要生硬切割
            - 每个镜头时长由情节节奏决定：开篇/结尾可较长（5-10s），高潮/动作镜头较短（1-3s）
            - `aigcPrompt` 只写当前镜头的画面描述（景别+视角+动作+环境），**不要** 把全局风格和角色关键词写进去（系统自动通过 characterIds/sceneId/propIds 注入）
            - 每个镜头的旁白或台词必须与时长匹配（中文约 5 字/秒）

            ## 输出格式
            仅以 JSON 格式返回，不要包含任何其他文字：
            {OutputJsonTemplate}
            """;
    }

    private const string OutputJsonTemplate = """
        {
          "title": "故事标题",
          "requirements": "需求说明（提炼用户意图，2-3句）",
          "synopsis": "故事梗概（角色+场景+道具+事件走向，3-5句话）",
          "wrappingStyle": "包装风格描述（整体美术方向：色调/字幕风格/画面氛围）",
          "characters": [
            { "id": "R0", "name": "旁白", "role": "叙述者", "voiceDescription": "温和沉稳，语速平缓" },
            { "id": "R1", "name": "主角名", "appearance": "详细外观描述（发型/服饰/体型/特征）", "role": "主角定位", "personality": "性格特点", "voiceDescription": "声音风格描述" }
          ],
          "scenes": [
            { "id": "L1", "name": "场景名称", "atmosphere": "氛围描述", "timeOfDay": "晨/暮/夜/日", "visualDescription": "详细视觉描述（背景/光线/色调）" }
          ],
          "props": [
            { "id": "P1", "name": "道具名称", "description": "形态描述", "visualEffect": "在画面中的动态效果描述" }
          ],
          "shots": [
            {
              "index": 1,
              "durationSeconds": 4,
              "frameType": "远景",
              "cameraAngle": "平视",
              "action": "主角站在场景中，镜头缓慢推近",
              "transition": "淡入",
              "characterIds": ["R1"],
              "sceneId": "L1",
              "propIds": ["P1"],
              "narrationOwner": "旁白",
              "voiceCharacterId": "R0",
              "narrationText": "旁白文本（完整句子，与时长匹配）",
              "characterDialogue": null,
              "aigcPrompt": "远景，平视视角，主角站立，镜头缓慢推近，场景中光线柔和"
            },
            {
              "index": 2,
              "durationSeconds": 1,
              "frameType": "中景",
              "cameraAngle": "平视",
              "action": "主角嘶喊",
              "transition": "切换",
              "characterIds": ["R1"],
              "sceneId": "L1",
              "propIds": [],
              "narrationOwner": "主角名",
              "voiceCharacterId": "R1",
              "narrationText": "",
              "characterDialogue": "角色台词！",
              "aigcPrompt": "中景，平视视角，主角张嘴嘶喊，表情激烈"
            }
          ],
          "notes": "制片说明（素材描述/画面风格要求/特殊制作说明）"
        }
        """;
}

// ─── 内部 DTO（仅用于反序列化 LLM 响应）────────────────────────────

internal class ScreenplayDto
{
    public string? Title { get; set; }
    public string? Requirements { get; set; }
    public string? Synopsis { get; set; }
    public string? WrappingStyle { get; set; }
    public List<CharacterSetupDto>? Characters { get; set; }
    public List<SceneSetupDto>? Scenes { get; set; }
    public List<PropSetupDto>? Props { get; set; }
    public List<ShotDto>? Shots { get; set; }
    public string? Notes { get; set; }
}

internal class CharacterSetupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Appearance { get; set; }
    public string? Role { get; set; }
    public string? Personality { get; set; }
    public string? VoiceDescription { get; set; }
}

internal class SceneSetupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Atmosphere { get; set; }
    public string? TimeOfDay { get; set; }
    public string? VisualDescription { get; set; }
}

internal class PropSetupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? VisualEffect { get; set; }
}

internal class ShotDto
{
    public int Index { get; set; }
    public int DurationSeconds { get; set; }
    public string? FrameType { get; set; }
    public string? CameraAngle { get; set; }
    public string? Action { get; set; }
    public string? Transition { get; set; }
    public string? AigcPrompt { get; set; }
    public string? NarrationText { get; set; }
    public string? NarrationOwner { get; set; }
    public string? VoiceCharacterId { get; set; }
    public string? CharacterDialogue { get; set; }
    public string? SceneId { get; set; }
    public string? SceneName { get; set; }
    public List<string>? CharacterIds { get; set; }
    public List<string>? PropIds { get; set; }
}
