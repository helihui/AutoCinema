using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline;

namespace AutoCinema.Pro.Services;

public interface IProjectService
{
    /// <summary>提交一个视频生成项目</summary>
    Task SubmitProjectAsync(VideoProject project);

    // 新增状态更新方法
    Task UpdateProjectStatusAsync(string projectId, ProjectStatus status);
    Task UpdateProjectProgressAsync(string projectId, ProductionProgress progress);
    Task UpdateProjectResultAsync(string projectId, string resultPath);
    Task UpdateProjectErrorAsync(string projectId, string errorMessage);

    /// <summary>获取项目状态和进度</summary>
    ProjectState? GetProjectState(string projectId);

    /// <summary>获取所有项目状态</summary>
    IEnumerable<ProjectState> GetAllProjectStates();
}
