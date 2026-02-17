using AutoCinema.Pro.Pipeline.Models;
using AutoCinema.Pro.Services.Editor;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Steps;

/// <summary>
/// 视频合成步骤
/// </summary>
/// <remarks>
/// <para>输入: SubtitleGenerationResult (字幕生成结果)</para>
/// <para>输出: VideoCompositionResult (视频合成结果)</para>
/// <para>前置步骤: SubtitleGenerationStep</para>
/// <para>后续步骤: 无</para>
/// </remarks>
public class VideoCompositionStep : BasePipelineStep<SubtitleGenerationResult, VideoCompositionResult>
{
    private readonly IVideoCompositionService _videoService;

    public VideoCompositionStep(IVideoCompositionService videoService)
    {
        _videoService = videoService;
    }

    public override string StepName => "视频合成";

    // 合成失败应立即终止,不支持重试
    public override bool CanRetry => false;

    public override async Task<VideoCompositionResult> ExecuteAsync(
        SubtitleGenerationResult input,
        PipelineStepContext context,
        CancellationToken ct)
    {
        LogStepStart(context);
        ReportProgress(context, "剪辑阶段", "合成视频", 85);

        context.Logger.LogInformation(
            "开始视频合成阶段: Stage={Stage}, TotalStages={TotalStages}",
            4, 4);

        var outputPath = Path.Combine(
            context.Project.OutputDirectory,
            $"{SanitizeFileName(context.Project.Title)}.mp4");

        await _videoService.ComposeAsync(input.Assets, input.SubtitlePath, outputPath, ct);

        // 获取文件信息
        var fileInfo = new FileInfo(outputPath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;

        context.Logger.LogInformation(
            "视频合成完成: OutputPath={OutputPath}, FileSize={FileSize}",
            outputPath, fileSize);

        LogStepComplete(context);

        return new VideoCompositionResult
        {
            OutputPath = outputPath,
            FileSize = fileSize,
            Duration = TimeSpan.Zero // TODO: 可以从 FFmpeg 获取实际时长
        };
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "output" : sanitized;
    }
}
