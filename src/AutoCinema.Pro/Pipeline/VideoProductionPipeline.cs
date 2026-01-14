using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Services.Actor;
using AutoCinema.Pro.Services.Director;
using AutoCinema.Pro.Services.Editor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Pipeline;

/// <summary>
/// 视频生产流水线实现
/// 编排导演层、演员层、剪辑层的完整工作流
/// </summary>
public class VideoProductionPipeline : IVideoProductionPipeline
{
    private readonly IStoryboardService _storyboardService;
    private readonly IImageGenerationService _imageService;
    private readonly ISpeechGenerationService _speechService;
    private readonly IAudioAnalysisService _audioService;
    private readonly ISubtitleService _subtitleService;
    private readonly IVideoCompositionService _videoService;
    private readonly PipelineOptions _options;
    private readonly ILogger<VideoProductionPipeline> _logger;

    public VideoProductionPipeline(
        IStoryboardService storyboardService,
        IImageGenerationService imageService,
        ISpeechGenerationService speechService,
        IAudioAnalysisService audioService,
        ISubtitleService subtitleService,
        IVideoCompositionService videoService,
        IOptions<PipelineOptions> options,
        ILogger<VideoProductionPipeline> logger)
    {
        _storyboardService = storyboardService;
        _imageService = imageService;
        _speechService = speechService;
        _audioService = audioService;
        _subtitleService = subtitleService;
        _videoService = videoService;
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> ProduceAsync(VideoProject project, CancellationToken ct = default)
    {
        return ProduceAsync(project, null, ct);
    }

    public async Task<string> ProduceAsync(
        VideoProject project,
        IProgress<ProductionProgress>? progress,
        CancellationToken ct = default)
    {
        var startTime = DateTime.Now;
        _logger.LogInformation("========================================");
        _logger.LogInformation("开始视频生产: {Title}", project.Title);
        _logger.LogInformation("项目 ID: {ProjectId}", project.ProjectId);
        _logger.LogInformation("========================================");

        // 确保输出目录存在
        Directory.CreateDirectory(project.OutputDirectory);

        try
        {
            // ========================================
            // 阶段 1/4: 导演阶段 - 解析故事板
            // ========================================
            progress?.Report(new ProductionProgress
            {
                Stage = "导演阶段",
                Step = "解析故事板",
                Percentage = 5
            });

            _logger.LogInformation("阶段 1/4: 解析故事板...");

            var storyboard = await _storyboardService.ParseAsync(
                project.RawStoryText,
                project.BaseVisualStyle,
                ct);

            _logger.LogInformation("解析完成，共 {Count} 个场景", storyboard.Scenes.Count);

            // ========================================
            // 阶段 2/4: 演员阶段 - 并行生成素材
            // ========================================
            progress?.Report(new ProductionProgress
            {
                Stage = "演员阶段",
                Step = "生成素材",
                Percentage = 10,
                TotalScenes = storyboard.Scenes.Count
            });

            _logger.LogInformation("阶段 2/4: 生成素材...");

            var assets = await GenerateAssetsAsync(project, storyboard, progress, ct);

            _logger.LogInformation("素材生成完成，共 {Count} 个素材", assets.Length);

            // ========================================
            // 阶段 3/4: 剪辑阶段 - 生成字幕
            // ========================================
            progress?.Report(new ProductionProgress
            {
                Stage = "剪辑阶段",
                Step = "生成字幕",
                Percentage = 80
            });

            _logger.LogInformation("阶段 3/4: 生成字幕...");

            var subtitlePath = Path.Combine(project.OutputDirectory, "subtitles.srt");
            await _subtitleService.GenerateSrtAsync(assets, subtitlePath, ct);

            // ========================================
            // 阶段 4/4: 剪辑阶段 - 合成视频
            // ========================================
            progress?.Report(new ProductionProgress
            {
                Stage = "剪辑阶段",
                Step = "合成视频",
                Percentage = 85
            });

            _logger.LogInformation("阶段 4/4: 合成视频...");

            var outputPath = Path.Combine(project.OutputDirectory, $"{SanitizeFileName(project.Title)}.mp4");
            await _videoService.ComposeAsync(assets, subtitlePath, outputPath, ct);

            // 完成
            var elapsed = DateTime.Now - startTime;
            progress?.Report(new ProductionProgress
            {
                Stage = "完成",
                Step = "视频已生成",
                Percentage = 100
            });

            _logger.LogInformation("========================================");
            _logger.LogInformation("视频生产完成!");
            _logger.LogInformation("输出路径: {Path}", outputPath);
            _logger.LogInformation("总耗时: {Elapsed:mm\\:ss}", elapsed);
            _logger.LogInformation("========================================");

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视频生产失败: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 并行生成素材（图片+音频）
    /// </summary>
    private async Task<GeneratedAsset[]> GenerateAssetsAsync(
        VideoProject project,
        Storyboard storyboard,
        IProgress<ProductionProgress>? progress,
        CancellationToken ct)
    {
        var totalScenes = storyboard.Scenes.Count;
        var completedCount = 0;

        var assetTasks = storyboard.Scenes.Select(async scene =>
        {
            var imagePath = Path.Combine(project.OutputDirectory, $"scene_{scene.Index:D3}.png");
            var audioPath = Path.Combine(project.OutputDirectory, $"scene_{scene.Index:D3}.mp3");
            var promptPath = Path.Combine(project.OutputDirectory, $"scene_{scene.Index:D3}.txt");

            _logger.LogDebug("开始生成场景 {Index}/{Total}", scene.Index, totalScenes);

            // 保存提示词到文件
            var promptContent = $"[Visual Prompt]\n{scene.VisualPrompt}\n\n[Speech Text]\n{scene.SpeechText}";
            await File.WriteAllTextAsync(promptPath, promptContent, ct);

            // 并行生成图片和音频
            var imageTask = _imageService.GenerateAsync(scene.VisualPrompt, imagePath, ct);
            var audioTask = _speechService.GenerateAsync(scene.SpeechText, audioPath, ct);

            await Task.WhenAll(imageTask, audioTask);

            // 获取音频精确时长（作为时间轴基准）
            var duration = await _audioService.GetDurationAsync(audioPath, ct);

            // 更新进度
            var completed = Interlocked.Increment(ref completedCount);
            var percentage = 10 + (int)(70.0 * completed / totalScenes);

            progress?.Report(new ProductionProgress
            {
                Stage = "演员阶段",
                Step = $"生成场景 {completed}/{totalScenes}",
                Percentage = percentage,
                CurrentScene = completed,
                TotalScenes = totalScenes
            });

            _logger.LogInformation("场景 {Index}/{Total} 完成 (时长: {Duration:mm\\:ss\\.fff})",
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

        return await Task.WhenAll(assetTasks);
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
