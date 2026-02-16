using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string _storyText = string.Empty;

    [ObservableProperty]
    private string _projectTitle = "新视频项目";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public ObservableCollection<ProjectState> RecentProjects { get; } = new();

    public MainWindowViewModel(IProjectService projectService, ILogger<MainWindowViewModel> logger)
    {
        _projectService = projectService;
        _logger = logger;
        LoadProjects();
    }

    [RelayCommand]
    private async Task SubmitProjectAsync()
    {
        _logger.LogInformation("用户点击了'开始生成视频'按钮");

        if (string.IsNullOrWhiteSpace(StoryText))
        {
            StatusMessage = "⚠️ 请先输入您的创意故事！";
            _logger.LogWarning("用户未输入故事文本");
            return;
        }

        IsProcessing = true;
        ProgressValue = 0;
        StatusMessage = "🚀 正在提交任务并初始化...";
        _logger.LogInformation("开始提交项目，标题: {Title}", ProjectTitle);

        var projectId = $"gui-{DateTime.Now:yyyyMMdd-HHmmss}";
        var project = new VideoProject
        {
            ProjectId = projectId,
            Title = ProjectTitle,
            RawStoryText = StoryText,
            OutputDirectory = "./output/gui"
        };

        try
        {
            await _projectService.SubmitProjectAsync(project);
            _logger.LogInformation("项目已提交: {ProjectId}", projectId);
            StatusMessage = "✅ 任务已提交，开始生成...";

            // 启动定时拉取进度的逻辑
            _ = PollProgressAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提交项目失败");
            StatusMessage = $"❌ 错误: {ex.Message}";
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ConfigureStyle()
    {
        _logger.LogInformation("用户点击了'风格配置'按钮");
        StatusMessage = "🎨 风格配置功能开发中...";
        // TODO: 打开风格配置对话框
    }

    private async Task PollProgressAsync(string projectId)
    {
        while (IsProcessing)
        {
            await Task.Delay(1000);
            var state = _projectService.GetProjectState(projectId);
            if (state == null) continue;

            if (state.Progress != null)
            {
                ProgressValue = state.Progress.Percentage;
                StatusMessage = $"{state.Progress.Stage}: {state.Progress.Step}";
            }

            if (state.Status == ProjectStatus.Completed || state.Status == ProjectStatus.Failed)
            {
                IsProcessing = false;
                if (state.Status == ProjectStatus.Failed)
                    StatusMessage = $"失败: {state.ErrorMessage}";
                else
                    StatusMessage = "生成成功！";

                LoadProjects();
                break;
            }
        }
    }

    private void LoadProjects()
    {
        var projects = _projectService.GetAllProjectStates()
            .OrderByDescending(p => p.CreatedAt)
            .Take(10);

        RecentProjects.Clear();
        foreach (var p in projects)
        {
            RecentProjects.Add(p);
        }
    }
}
