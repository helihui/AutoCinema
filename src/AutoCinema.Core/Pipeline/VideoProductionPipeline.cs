using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Data;
using AutoCinema.Pro.Models;
using Microsoft.EntityFrameworkCore;
using AutoCinema.Pro.Pipeline.Cache;
using AutoCinema.Pro.Pipeline.Configuration;
using AutoCinema.Pro.Pipeline.Metrics;
using AutoCinema.Pro.Pipeline.Models;
using AutoCinema.Pro.Pipeline.Steps;
using AutoCinema.Pro.Pipeline.Visualization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro.Pipeline;

/// <summary>
/// 视频生产流水线实现
/// 编排导演层、演员层、剪辑层的完整工作流
/// </summary>
public class VideoProductionPipeline : IVideoProductionPipeline
{
    private readonly ILogger<VideoProductionPipeline> _logger;
    private readonly IDbContextFactory<CinemaDbContext> _dbContextFactory;
    private readonly PipelineConfigurationLoader _configLoader;
    private readonly StoryboardParsingStep _storyboardParsingStep;
    private readonly AssetAggregationStep _assetAggregationStep;
    private readonly SubtitleGenerationStep _subtitleGenerationStep;
    private readonly VideoCompositionStep _videoCompositionStep;

    public VideoProductionPipeline(
        ILogger<VideoProductionPipeline> logger,
        IDbContextFactory<CinemaDbContext> dbContextFactory,
        PipelineConfigurationLoader configLoader,
        StoryboardParsingStep storyboardParsingStep,
        AssetAggregationStep assetAggregationStep,
        SubtitleGenerationStep subtitleGenerationStep,
        VideoCompositionStep videoCompositionStep)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _configLoader = configLoader;
        _storyboardParsingStep = storyboardParsingStep;
        _assetAggregationStep = assetAggregationStep;
        _subtitleGenerationStep = subtitleGenerationStep;
        _videoCompositionStep = videoCompositionStep;
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

            // 加载 Pipeline 配置
            var configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "pipeline.json");
            var config = await _configLoader.LoadFromFileAsync(configPath);

            _logger.LogInformation("Pipeline 配置: {ConfigName} (v{Version})", config.Name, config.Version);

            // 启用步骤缓存 (数据库)
            context.SetCache(new DbStepCache<VideoProject, StoryboardResult>(_dbContextFactory, _logger));
            context.SetCache(new DbStepCache<StoryboardResult, AssetGenerationResult>(_dbContextFactory, _logger));
            context.SetCache(new DbStepCache<AssetGenerationResult, SubtitleGenerationResult>(_dbContextFactory, _logger));
            context.SetCache(new DbStepCache<SubtitleGenerationResult, VideoCompositionResult>(_dbContextFactory, _logger));

            // 使用类型安全的 Pipeline Builder 执行所有步骤
            // 注意: 由于类型链式特性,步骤顺序是固定的,配置主要用于记录和未来扩展
            var result = await new PipelineBuilder<VideoProject>()
                .AddStep(_storyboardParsingStep)   // VideoProject -> StoryboardResult
                .AddStep(_assetAggregationStep)     // StoryboardResult -> AssetGenerationResult
                .AddStep(_subtitleGenerationStep)  // AssetGenerationResult -> SubtitleGenerationResult
                .AddStep(_videoCompositionStep)    // SubtitleGenerationResult -> VideoCompositionResult
                .ExecuteAsync(context, ct);

            // 完成
            var elapsed = DateTime.Now - startTime;
            progress?.Report(new ProductionProgress
            {
                Stage = "完成",
                Step = "视频生成完成",
                Percentage = 100
            });

            // 生成性能报告
            var report = new PipelineExecutionReport(context.Metrics);
            _logger.LogInformation(report.GenerateReport());

            // 生成可视化报告
            var visualizer = new PipelineVisualizer();
            var visualReport = visualizer.GenerateFullReport(report, context.Metrics);

            // 保存到文件
            var reportPath = Path.Combine(project.OutputDirectory, "pipeline_report.md");
            await File.WriteAllTextAsync(reportPath, visualReport);

            _logger.LogInformation("可视化报告已保存: {ReportPath}", reportPath);

            _logger.LogInformation("========================================");
            _logger.LogInformation("视频生成成功!");
            _logger.LogInformation("输出路径: {OutputPath}", result.OutputPath);
            _logger.LogInformation("总耗时: {Elapsed}", elapsed);
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
