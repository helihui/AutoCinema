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

        var state = new ProjectState { ProjectId = project.ProjectId, Type = project.Type };
        context.Projects.Add(state);
        await context.SaveChangesAsync();

        _logger.LogInformation("项目 {ProjectId} 已提交到数据库", project.ProjectId);
    }

    public async Task UpdateProjectStatusAsync(string projectId, ProjectStatus status)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var state = await context.Projects.FindAsync(projectId);
        if (state != null)
        {
            state.Status = status;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateProjectProgressAsync(string projectId, ProductionProgress progress)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var state = await context.Projects.FindAsync(projectId);
        if (state != null)
        {
            state.Progress = progress;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateProjectResultAsync(string projectId, string resultPath)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var state = await context.Projects.FindAsync(projectId);
        if (state != null)
        {
            state.ResultPath = resultPath;
            state.Status = ProjectStatus.Completed;
            state.FinishedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            _logger.LogInformation("项目 {ProjectId} 标记为完成", projectId);
        }
    }

    public async Task UpdateProjectErrorAsync(string projectId, string errorMessage)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var state = await context.Projects.FindAsync(projectId);
        if (state != null)
        {
            state.Status = ProjectStatus.Failed;
            state.ErrorMessage = errorMessage;
            state.FinishedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            _logger.LogError("项目 {ProjectId} 标记为失败: {Error}", projectId, errorMessage);
        }
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
