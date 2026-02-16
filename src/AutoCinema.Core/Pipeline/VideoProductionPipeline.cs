using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline.Steps;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Pipeline;

/// <summary>
/// 视频生产流水线实现
/// 编排导演层、演员层、剪辑层的完整工作流
/// </summary>
public class VideoProductionPipeline : IVideoProductionPipeline
{
    private readonly StoryboardParsingStep _storyboardParsingStep;
    private readonly AssetGenerationStep _assetGenerationStep;
    private readonly SubtitleGenerationStep _subtitleGenerationStep;
    private readonly VideoCompositionStep _videoCompositionStep;
    private readonly ILogger<VideoProductionPipeline> _logger;

    public VideoProductionPipeline(
        StoryboardParsingStep storyboardParsingStep,
        AssetGenerationStep assetGenerationStep,
        SubtitleGenerationStep subtitleGenerationStep,
        VideoCompositionStep videoCompositionStep,
        ILogger<VideoProductionPipeline> logger)
    {
        _storyboardParsingStep = storyboardParsingStep;
        _assetGenerationStep = assetGenerationStep;
        _subtitleGenerationStep = subtitleGenerationStep;
        _videoCompositionStep = videoCompositionStep;
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
            // 创建执行上下文
            var context = new PipelineStepContext
            {
                Project = project,
                Progress = progress,
                Logger = _logger,
                SharedData = new Dictionary<string, object>
                {
                    ["LastOutput"] = project // 初始输入
                }
            };

            // 使用类型安全的 Pipeline Builder 执行所有步骤
            var result = await new PipelineBuilder<VideoProject>()
                .AddStep(_storyboardParsingStep)   // VideoProject -> StoryboardResult
                .AddStep(_assetGenerationStep)     // StoryboardResult -> AssetGenerationResult
                .AddStep(_subtitleGenerationStep)  // AssetGenerationResult -> SubtitleGenerationResult
                .AddStep(_videoCompositionStep)    // SubtitleGenerationResult -> VideoCompositionResult
                .ExecuteAsync(context, ct);

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
            _logger.LogInformation("输出路径: {Path}", result.OutputPath);
            _logger.LogInformation("文件大小: {Size:N0} bytes", result.FileSize);
            _logger.LogInformation("总耗时: {Elapsed:mm\\:ss}", elapsed);
            _logger.LogInformation("========================================");

            return result.OutputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视频生产失败: {Message}", ex.Message);
            throw;
        }
    }
}
