using AutoCinema.Pro.Pipeline.Models;
using AutoCinema.Pro.Services.Editor;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// 字幕生成步骤
/// </summary>
/// <remarks>
/// <para>输入: AssetGenerationResult (素材生成结果)</para>
/// <para>输出: SubtitleGenerationResult (字幕生成结果)</para>
/// <para>前置步骤: AssetGenerationStep</para>
/// <para>后续步骤: VideoCompositionStep</para>
/// </remarks>
public class SubtitleGenerationStep : BasePipelineStep<AssetGenerationResult, SubtitleGenerationResult>
{
    private readonly ISubtitleService _subtitleService;

    public SubtitleGenerationStep(ISubtitleService subtitleService)
    {
        _subtitleService = subtitleService;
    }

    public override string StepName => "字幕生成";

    // 支持重试
    public override bool CanRetry => true;
    public override int MaxRetries => 2;

    public override async Task<SubtitleGenerationResult> ExecuteAsync(
        AssetGenerationResult input,
        PipelineStepContext context,
        CancellationToken ct)
    {
        LogStepStart(context);
        ReportProgress(context, "剪辑阶段", "生成字幕", 80);

        context.Logger.LogInformation(
            "开始字幕生成阶段: Stage={Stage}, TotalStages={TotalStages}",
            3, 4);

        var subtitlePath = Path.Combine(context.Project.OutputDirectory, "subtitles.srt");
        await _subtitleService.GenerateSrtAsync(input.Assets, subtitlePath, ct);

        context.Logger.LogInformation(
            "字幕生成完成: SubtitlePath={SubtitlePath}, SubtitleCount={SubtitleCount}",
            subtitlePath, input.Assets.Length);

        LogStepComplete(context);

        return new SubtitleGenerationResult
        {
            SubtitlePath = subtitlePath,
            SubtitleCount = input.Assets.Length,
            Assets = input.Assets // 传递给下一步
        };
    }
}
