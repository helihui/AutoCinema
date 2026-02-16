using AutoCinema.Pro.Data;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Services;

public class ProjectService : IProjectService
{
    private readonly IDbContextFactory<CinemaDbContext> _contextFactory;
    private readonly IVideoProductionPipeline _pipeline;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IDbContextFactory<CinemaDbContext> contextFactory, IVideoProductionPipeline pipeline, ILogger<ProjectService> logger)
    {
        _contextFactory = contextFactory;
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task SubmitProjectAsync(VideoProject project)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var state = new ProjectState { ProjectId = project.ProjectId };
        context.Projects.Add(state);
        await context.SaveChangesAsync();

        // 后台视频生成任务
        _ = Task.Run(async () =>
        {
            try
            {
                using var innerContext = await _contextFactory.CreateDbContextAsync();
                var trackedState = await innerContext.Projects.FindAsync(project.ProjectId);
                if (trackedState == null) return;

                trackedState.Status = ProjectStatus.Processing;
                await innerContext.SaveChangesAsync();

                var progress = new Progress<ProductionProgress>(p =>
                {
                    // 注意：这里频繁更新数据库可能会有性能压力，但在单机工具中是可以接受的。
                    // 为了性能，可以考虑节流更新，这里先实现最直接的。
                    using var updateContext = _contextFactory.CreateDbContext();
                    var s = updateContext.Projects.Find(project.ProjectId);
                    if (s != null)
                    {
                        s.Progress = p;
                        updateContext.SaveChanges();
                    }
                });

                var resultPath = await _pipeline.ProduceAsync(project, progress);

                trackedState.ResultPath = resultPath;
                trackedState.Status = ProjectStatus.Completed;
                trackedState.FinishedAt = DateTime.UtcNow;
                await innerContext.SaveChangesAsync();

                _logger.LogInformation("项目 {ProjectId} 数据库记录已更新为成功: {ResultPath}", project.ProjectId, resultPath);
            }
            catch (Exception ex)
            {
                using var errorContext = await _contextFactory.CreateDbContextAsync();
                var errorState = await errorContext.Projects.FindAsync(project.ProjectId);
                if (errorState != null)
                {
                    errorState.Status = ProjectStatus.Failed;
                    errorState.ErrorMessage = ex.Message;
                    errorState.FinishedAt = DateTime.UtcNow;
                    await errorContext.SaveChangesAsync();
                }
                _logger.LogError(ex, "项目 {ProjectId} 数据库记录已更新为失败", project.ProjectId);
            }
        });
    }

    public ProjectState? GetProjectState(string projectId)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Projects.AsNoTracking().FirstOrDefault(p => p.ProjectId == projectId);
    }

    public IEnumerable<ProjectState> GetAllProjectStates()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Projects.AsNoTracking().ToList();
    }
}
