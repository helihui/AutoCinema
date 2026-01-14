using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Pipeline;

/// <summary>
/// 视频生产流水线接口
/// </summary>
public interface IVideoProductionPipeline
{
    /// <summary>
    /// 执行完整的视频生产流程
    /// </summary>
    /// <param name="project">视频项目配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的视频文件路径</returns>
    Task<string> ProduceAsync(VideoProject project, CancellationToken ct = default);

    /// <summary>
    /// 执行完整的视频生产流程（带进度回调）
    /// </summary>
    /// <param name="project">视频项目配置</param>
    /// <param name="progress">进度报告回调</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的视频文件路径</returns>
    Task<string> ProduceAsync(VideoProject project, IProgress<ProductionProgress>? progress, CancellationToken ct = default);
}

/// <summary>
/// 生产进度信息
/// </summary>
public record ProductionProgress
{
    /// <summary>当前阶段</summary>
    public required string Stage { get; init; }

    /// <summary>当前步骤</summary>
    public required string Step { get; init; }

    /// <summary>进度百分比 (0-100)</summary>
    public int Percentage { get; init; }

    /// <summary>当前场景索引</summary>
    public int? CurrentScene { get; init; }

    /// <summary>总场景数</summary>
    public int? TotalScenes { get; init; }
}
