using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Services.Editor;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Core.Services.Jobs;

/// <summary>
/// 幻灯片任务处理器
/// </summary>
public class SlideshowJobHandler : IJobHandler
{
    private readonly IVideoCompositionService _videoService;
    private readonly ILogger<SlideshowJobHandler> _logger;

    public SlideshowJobHandler(
        IVideoCompositionService videoService,
        ILogger<SlideshowJobHandler> logger)
    {
        _videoService = videoService;
        _logger = logger;
    }

    public JobType SupportedType => JobType.ImageToVideo;

    public async Task ExecuteAsync(JobItem job, IProgress<ProductionProgress> progress, CancellationToken ct)
    {
        if (job is not SlideshowJob slideshowJob)
        {
            throw new ArgumentException("Job must be of type SlideshowJob", nameof(job));
        }

        _logger.LogInformation("开始处理幻灯片任务: {JobId}, {Count} 张幻灯片", job.JobId, slideshowJob.Items.Count);

        // 1. 准备素材
        var assets = new List<GeneratedAsset>();
        int index = 0;

        foreach (var item in slideshowJob.Items)
        {
            // 如果没有音频，可能需要生成静音音频
            // 目前 FFMpegVideoService 依赖 AudioPath，所以我们暂时要求必须有音频
            // 或者我们可以生成一个临时的静音文件？
            // 简单起见，如果音频为空，我们抛出异常或记录警告
            // 为了支持纯图片，我们需要在 FFMpegVideoService 中处理空音频，或者在这里生成静音
            // 这里我们假设 FFMpegVideoService 暂时需要音频。
            // TODO: 生成静音音频支持

            if (string.IsNullOrEmpty(item.AudioPath))
            {
                // 暂时不支持空音频
                // _logger.LogWarning("幻灯片 {Index} 缺少音频，可能会导致生成的视频无声或异常", index);
                // 实际上我们可以生成一个假的 GeneratedAsset，其 AudioPath 指向一个通用静音文件
                // 但这需要 ffmpeg 生成。
                // 既然是 MVP，我们先假定有音频，或者用户接受失败。
                // 或者使用 BackgroundMusicPath 覆盖？
                // 如果有 BackgroundMusicPath，我们其实不需要每个 slide 的音频。
                // 但 Service API 是 per-segment 的。
            }

            assets.Add(new GeneratedAsset
            {
                SceneIndex = index++,
                ImagePath = item.ImagePath,
                AudioPath = item.AudioPath ?? string.Empty, // 传递空字符串，依赖 Service 处理或报错
                AudioDuration = TimeSpan.FromSeconds(item.Duration > 0 ? item.Duration : 3.0),
                SpeechText = string.Empty // 幻灯片没有台词文本
            });
        }

        progress.Report(new ProductionProgress { Stage = "Processing", Step = "合成视频中", Percentage = 50 });

        // 2. 调用合成服务
        // 字幕路径传空字符串，表示不需要字幕
        var outputPath = await _videoService.ComposeAsync(assets, string.Empty, job.OutputPath ?? GenerateOutputPath(job), ct);

        // 3. 处理背景音乐 (如果设置了)
        if (!string.IsNullOrEmpty(slideshowJob.BackgroundMusicPath))
        {
            // TODO: 混音背景音乐
            // 由于 IVideoCompositionService 接口限制，目前无法直接传递背景音乐。
            // 需要后续扩展 Service 或在此处进行二次处理。
            _logger.LogWarning("BackgroundMusicPath 已设置但尚未支持混音，生成的视频将保留原始音频片段。");
        }

        job.OutputPath = outputPath;
        job.FinishedAt = DateTime.UtcNow;
        job.Status = JobStatus.Completed;
        job.Progress = new ProductionProgress { Stage = "Completed", Step = "Done", Percentage = 100 };

        _logger.LogInformation("幻灯片任务完成: {OutputPath}", outputPath);
    }

    private string GenerateOutputPath(JobItem job)
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "Slideshows");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"Slideshow_{job.JobId}_{DateTime.Now:yyyyMMddHHmmss}.mp4");
    }
}
