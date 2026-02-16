using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Services;

public interface IProjectService
{
    /// <summary>提交一个视频生成项目</summary>
    Task SubmitProjectAsync(VideoProject project);

    /// <summary>获取项目状态和进度</summary>
    ProjectState? GetProjectState(string projectId);

    /// <summary>获取所有项目状态</summary>
    IEnumerable<ProjectState> GetAllProjectStates();
}
