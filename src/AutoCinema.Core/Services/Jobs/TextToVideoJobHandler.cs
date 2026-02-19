using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Services;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Core.Services.Jobs;

public class TextToVideoJobHandler : IJobHandler
{
    private readonly IVideoProductionPipeline _pipeline;
    private readonly IProjectService _projectService;
    private readonly ILogger<TextToVideoJobHandler> _logger;

    public JobType SupportedType => JobType.TextOnVideo;

    public TextToVideoJobHandler(
        IVideoProductionPipeline pipeline,
        IProjectService projectService,
        ILogger<TextToVideoJobHandler> logger)
    {
        _pipeline = pipeline;
        _projectService = projectService;
        _logger = logger;
    }

    public async Task ExecuteAsync(JobItem job, IProgress<ProductionProgress> progress, CancellationToken ct)
    {
        if (job is not TextToVideoJob textJob)
        {
            throw new ArgumentException($"Job type {job.Type} is not supported by TextToVideoJobHandler");
        }

        var project = textJob.ProjectData;
        _logger.LogInformation("Processing TextToVideo Job: {JobId} for Project: {ProjectId}", job.JobId, project.ProjectId);

        // Update Project Status to Processing
        await _projectService.UpdateProjectStatusAsync(project.ProjectId, ProjectStatus.Processing);

        try
        {
            // Update Project Progress proxy
            var projectProgress = new Progress<ProductionProgress>(p =>
            {
                // This updates the 'VideoProject' specific progress in DB if needed
                _projectService.UpdateProjectProgressAsync(project.ProjectId, p).Wait();

                // Report back to the JobManager's progress tracker
                progress.Report(p);
            });

            var resultPath = await _pipeline.ProduceAsync(project, projectProgress, ct);

            // Update Project Result
            await _projectService.UpdateProjectResultAsync(project.ProjectId, resultPath);
            job.OutputPath = resultPath;
        }
        catch (Exception ex)
        {
            await _projectService.UpdateProjectErrorAsync(project.ProjectId, ex.Message);
            throw;
        }
    }
}
