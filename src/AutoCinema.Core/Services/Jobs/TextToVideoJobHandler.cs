using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Services;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Core.Services.Jobs;

public class TextToVideoJobHandler : IJobHandler
{
    private readonly IVideoProductionPipeline _pipeline;
    private readonly ILogger<TextToVideoJobHandler> _logger;

    public JobType SupportedType => JobType.TextOnVideo;

    public TextToVideoJobHandler(
        IVideoProductionPipeline pipeline,
        ILogger<TextToVideoJobHandler> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task ExecuteAsync(JobItem job, IProgress<ProductionProgress> progress, CancellationToken ct)
    {
        if (job is not TextToVideoJob textJob)
            throw new ArgumentException($"Job type {job.Type} is not supported by TextToVideoJobHandler");

        var project = textJob.ProjectData;
        _logger.LogInformation("Processing TextToVideo Job: {JobId} for Project: {ProjectId}", job.JobId, project.ProjectId);

        try
        {
            // 用包装 Progress 拦截 ReviewGate 信号
            var interceptProgress = new Progress<ProductionProgress>(p =>
            {
                // 检测 CoDesign ReviewGate：设置任务状态为 WaitingForReview
                if (p.ReviewGate != null && !job.HasPendingReview)
                {
                    _logger.LogInformation("CoDesign ReviewGate 检测到，Job {JobId} 进入 WaitingForReview", job.JobId);
                    job.ReviewGate = p.ReviewGate;
                    job.Status = JobStatus.WaitingForReview;
                }

                job.Progress = p;
                progress.Report(p);
            });

            // 若有 ReviewGate，pipeline 内部已经通过 Progress 传出来了。
            // 但 pipeline 本身需要等 gate 解锁后才能收到最终剧本继续运行（gate.WaitForApprovalAsync 由 ScreenplayGenerationStep 调用）。
            // 所以 ProduceAsync 会在等待过程中长时间阻塞，直到用户 Approve 或 Cancel。
            var resultPath = await _pipeline.ProduceAsync(project, interceptProgress, ct);

            job.OutputPath = resultPath;
            job.ReviewGate = null; // 清除 gate
        }
        catch (OperationCanceledException)
        {
            // 用户 Cancel ReviewGate 会导致 pipeline 抛出 OperationCanceledException
            job.ReviewGate = null;
            job.Status = JobStatus.Cancelled;
            _logger.LogInformation("Job {JobId} 因用户取消审阅而终止", job.JobId);
            throw; // 让 JobManager 处理状态收尾
        }
        catch (Exception ex)
        {
            job.ReviewGate = null;
            throw;
        }
    }
}
