using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline.Models;
using AutoCinema.Pro.Services.Director;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// 故事板解析步骤
/// </summary>
/// <remarks>
/// <para>输入: VideoProject (视频项目)</para>
/// <para>输出: StoryboardResult (故事板解析结果)</para>
/// <para>前置步骤: 无</para>
/// <para>后续步骤: AssetGenerationStep</para>
/// </remarks>
public class StoryboardParsingStep : BasePipelineStep<VideoProject, StoryboardResult>
{
    private readonly IStoryboardService _storyboardService;

    public StoryboardParsingStep(IStoryboardService storyboardService)
    {
        _storyboardService = storyboardService;
    }

    public override string StepName => "故事板解析";

    // 解析失败应立即终止,不支持重试
    public override bool CanRetry => false;

    public override async Task<StoryboardResult> ExecuteAsync(
        VideoProject input,
        PipelineStepContext context,
        CancellationToken ct)
    {
        LogStepStart(context);
        ReportProgress(context, "导演阶段", "解析故事板", 5);

        context.Logger.LogInformation("阶段 1/4: 解析故事板...");

        var storyboard = await _storyboardService.ParseAsync(
            input.RawStoryText,
            input.BaseVisualStyle,
            ct);

        context.Logger.LogInformation("解析完成,共 {Count} 个场景", storyboard.Scenes.Count);

        LogStepComplete(context);

        return new StoryboardResult
        {
            Storyboard = storyboard,
            SceneCount = storyboard.Scenes.Count
        };
    }
}
