using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Screenplay;
using AutoCinema.Pro.Pipeline.Models;
using AutoCinema.Pro.Services.Screenplay;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// 剧本生成步骤（AC-Node: StoryGenerator + ShotPlanner + PromptBuilder 合一）
/// 
/// 输入: VideoProject（含 RawStoryText、ProjectConfig、CharacterAnchors 等）
/// 输出: ScreenplayResult（含完整剧本 + 可选 ReviewGate）
/// 
/// CoDesign 流程：
///   若 StoryConfig.CoDesign=true，步骤完成后创建 ReviewGate
///   并将 ProjectStatus 设为 WaitingForReview，下游步骤 await 关卡解锁
/// </summary>
public class ScreenplayGenerationStep : BasePipelineStep<VideoProject, ScreenplayResult>
{
    private readonly ScreenplayGeneratorService _generatorService;

    public ScreenplayGenerationStep(ScreenplayGeneratorService generatorService)
    {
        _generatorService = generatorService;
    }

    public override string StepName => "剧本生成";
    public override bool CanRetry => false;

    public override async Task<ScreenplayResult> ExecuteAsync(
        VideoProject input,
        PipelineStepContext context,
        CancellationToken ct)
    {
        LogStepStart(context);
        ReportProgress(context, "编剧阶段", "生成完整剧本", 5);

        context.Logger.LogInformation(
            "开始全量剧本生成: Title={Title}, Duration={Duration}s",
            input.Title, input.ProjectConfig?.TotalDurationSeconds ?? 60);

        // 使用项目配置，若无则使用默认值
        var config = input.ProjectConfig ?? new ProjectConfig
        {
            TotalDurationSeconds = 60,
            StyleTheme = input.BaseVisualStyle ?? ""
        };

        var screenplay = await _generatorService.GenerateAsync(
            storyIntent: input.RawStoryText,
            config: config,
            characterAnchors: input.CharacterAnchors,
            sceneSetups: input.SceneSetups,
            propSetups: input.PropSetups,
            ct: ct);

        context.Logger.LogInformation(
            "剧本生成完成: ShotCount={ShotCount}, TotalDuration={Duration}s",
            screenplay.Shots.Count,
            screenplay.Shots.Sum(s => s.DurationSeconds));

        ReportProgress(context, "编剧阶段", $"剧本已生成（{screenplay.Shots.Count} 个分镜）", 30);

        // ── CoDesign 模式：解析首行配置中的 codesign 标志 ──────────────
        var coDesign = IsCoDesignEnabled(input.RawStoryText);
        StoryboardReviewGate? gate = null;

        if (coDesign)
        {
            gate = new StoryboardReviewGate(screenplay);
            context.Logger.LogInformation("CoDesign 模式：剧本已暂停，等待用户在界面上审阅并确认分镜");

            // 通过 Progress 将 ReviewGate 传递给 Handler 层，Handler 会设置 Job.ReviewGate 并暂停队列
            context.Progress?.Report(new ProductionProgress
            {
                Stage = "等待审阅",
                Step = "🎬 剧本已就绪，请查看并确认分镜内容",
                Percentage = 30,
                ReviewGate = gate
            });
        }

        LogStepComplete(context);

        return new ScreenplayResult
        {
            Screenplay = screenplay,
            ReviewGate = gate
        };
    }

    private static bool IsCoDesignEnabled(string rawText)
    {
        var firstLine = rawText.Split('\n', 2)[0].Trim();
        if (!firstLine.StartsWith("{{") || !firstLine.EndsWith("}}")) return false;

        try
        {
            var inner = firstLine[2..^2].Trim();
            var configMatch = System.Text.RegularExpressions.Regex.Match(
                inner, @"codesign\s*:\s*(true|false)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return configMatch.Success &&
                   configMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
