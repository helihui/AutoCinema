using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline.Models;
using AutoCinema.Pro.Services.Actor;
using AutoCinema.Pro.Services.Editor;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// 素材聚合步骤
/// </summary>
/// <remarks>
/// <para>输入: StoryboardResult (故事板解析结果)</para>
/// <para>输出: AssetGenerationResult (素材生成结果)</para>
/// <para>职责: 并行调用图片和音频生成,聚合结果</para>
/// <para>前置步骤: StoryboardParsingStep</para>
/// <para>后续步骤: SubtitleGenerationStep</para>
/// </remarks>
public class AssetAggregationStep : BasePipelineStep<StoryboardResult, AssetGenerationResult>
{
    private readonly IImageGenerationService _imageService;
    private readonly ISpeechGenerationService _speechService;
    private readonly IAudioAnalysisService _audioService;

    public AssetAggregationStep(
        IImageGenerationService imageService,
        ISpeechGenerationService speechService,
        IAudioAnalysisService audioService)
    {
        _imageService = imageService;
        _speechService = speechService;
        _audioService = audioService;
    }

    public override string StepName => "素材聚合";

    // 支持重试
    public override bool CanRetry => true;
    public override int MaxRetries => 3;

    public override async Task<AssetGenerationResult> ExecuteAsync(
        StoryboardResult input,
        PipelineStepContext context,
        CancellationToken ct)
    {
        LogStepStart(context);
        ReportProgress(context, "演员阶段", "生成素材", 10);

        context.Logger.LogInformation(
            "开始素材生成阶段: Stage={Stage}, TotalStages={TotalStages}",
            2, 4);

        var storyboard = input.Storyboard;
        var totalScenes = storyboard.Scenes.Count;
        var completedCount = 0;

        var assetTasks = storyboard.Scenes.Select(async scene =>
        {
            var imagePath = Path.Combine(context.Project.OutputDirectory, $"scene_{scene.Index:D3}.png");
            var audioPath = Path.Combine(context.Project.OutputDirectory, $"scene_{scene.Index:D3}.mp3");
            var promptPath = Path.Combine(context.Project.OutputDirectory, $"scene_{scene.Index:D3}.txt");

            context.Logger.LogDebug(
                "开始生成场景: SceneIndex={SceneIndex}, TotalScenes={TotalScenes}",
                scene.Index, totalScenes);

            // 保存提示词到文件
            var promptContent = $"[Visual Prompt]\n{scene.VisualPrompt}\n\n[Speech Text]\n{scene.SpeechText}";
            await File.WriteAllTextAsync(promptPath, promptContent, ct);

            // 并行生成图片和音频
            var imageTask = _imageService.GenerateAsync(scene.VisualPrompt, imagePath, ct);
            var audioTask = _speechService.GenerateAsync(scene.SpeechText, audioPath, ct);

            await Task.WhenAll(imageTask, audioTask);

            // 获取音频精确时长(作为时间轴基准)
            var duration = await _audioService.GetDurationAsync(audioPath, ct);

            // 更新进度
            var completed = Interlocked.Increment(ref completedCount);
            var percentage = 10 + (int)(70.0 * completed / totalScenes);

            ReportProgress(context, "演员阶段", $"生成场景 {completed}/{totalScenes}", percentage);

            context.Logger.LogInformation(
                "场景生成完成: SceneIndex={SceneIndex}, TotalScenes={TotalScenes}, Duration={Duration}",
                scene.Index, totalScenes, duration);

            return new GeneratedAsset
            {
                SceneIndex = scene.Index,
                ImagePath = imagePath,
                AudioPath = audioPath,
                AudioDuration = duration,
                SpeechText = scene.SpeechText
            };
        });

        var assets = await Task.WhenAll(assetTasks);
        var totalDuration = TimeSpan.FromMilliseconds(assets.Sum(a => a.AudioDuration.TotalMilliseconds));

        context.Logger.LogInformation(
            "素材生成完成: AssetCount={AssetCount}, TotalDuration={TotalDuration}",
            assets.Length, totalDuration);

        LogStepComplete(context);

        return new AssetGenerationResult
        {
            Assets = assets,
            TotalDuration = totalDuration
        };
    }
}
