using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Services;
using AutoCinema.Pro.Services.Actor;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

using AutoCinema.Desktop.Services;

namespace AutoCinema.Desktop.ViewModels;


public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IVoiceConfigurationService _voiceConfigService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IJobManager _jobManager;
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

    [ObservableProperty]
    private string _visualStyle = "Cinematic, high quality, detailed, professional lighting";

    [ObservableProperty]
    private string _characterPrompt = "";

    // 声音配置相关属性
    [ObservableProperty]
    private ObservableCollection<VoiceProfile> _availableVoices = new();

    [ObservableProperty]
    private VoiceProfile? _selectedVoice;

    [ObservableProperty]
    private double _voiceSpeed = 1.0;

    [ObservableProperty]
    private int _voicePitch = 0;

    [ObservableProperty]
    private string _voiceEmotion = "neutral";

    /// <summary>是否支持声音克隆</summary>
    public bool SupportsVoiceCloning => _voiceConfigService?.SupportsVoiceCloning ?? false;

    public System.Collections.Generic.List<string> Emotions { get; } = new()
    {
        "neutral",
        "happy",
        "sad",
        "angry",
        "surprised"
    };

    public ObservableCollection<ProjectState> RecentProjects { get; } = new();

    public MainWindowViewModel(
        IProjectService projectService,
        IVoiceConfigurationService voiceConfigService,
        IAudioPlayerService audioPlayerService,
        IJobManager jobManager,
        ILogger<MainWindowViewModel> logger)
    {
        _projectService = projectService;
        _voiceConfigService = voiceConfigService;
        _audioPlayerService = audioPlayerService;
        _jobManager = jobManager;
        _logger = logger;
        LoadProjects();
        _ = LoadVoicesAsync(); // 异步加载声音列表
    }

    [RelayCommand]
    private async Task SubmitProjectAsync()
    {
        _logger.LogInformation("用户点击了'开始生成视频'按钮");

        if (string.IsNullOrWhiteSpace(StoryText))
        {
            StatusMessage = "⚠️ 请先输入您的创意故事!";
            _logger.LogWarning("用户未输入故事文本");
            return;
        }

        IsProcessing = true;
        ProgressValue = 0;
        StatusMessage = "🚀 正在提交任务并初始化...";
        _logger.LogInformation("开始提交项目,标题: {Title}", ProjectTitle);

        var projectId = $"gui-{DateTime.Now:yyyyMMdd-HHmmss}";
        var project = new VideoProject
        {
            ProjectId = projectId,
            Title = ProjectTitle,
            RawStoryText = StoryText,
            OutputDirectory = "./output/gui",
            BaseVisualStyle = VisualStyle,
            CharacterPrompt = CharacterPrompt,
            VoiceConfig = SelectedVoice != null ? new VoiceGenerationConfig
            {
                VoiceId = SelectedVoice.VoiceId,
                Speed = VoiceSpeed,
                Pitch = VoicePitch,
                Emotion = VoiceEmotion
            } : null
        };

        try
        {
            await _projectService.SubmitProjectAsync(project);

            // 创建并提交任务
            var job = new TextToVideoJob
            {
                ProjectData = project,
                Title = project.Title,
                CorrelationId = project.ProjectId
            };
            await _jobManager.EnqueueAsync(job);

            _jobManager.StartProcessing();

            _logger.LogInformation("项目已提交并加入队列: {ProjectId}", projectId);
            StatusMessage = "✅ 任务已加入队列，请在任务管理窗口查看进度";

            // 重置状态
            IsProcessing = false;
            LoadProjects();
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

    /// <summary>
    /// 加载可用声音列表
    /// </summary>
    private async Task LoadVoicesAsync()
    {
        try
        {
            _logger.LogInformation("正在加载声音列表...");
            var voices = await _voiceConfigService.GetAvailableVoicesAsync();

            AvailableVoices.Clear();
            foreach (var voice in voices)
            {
                AvailableVoices.Add(voice);
            }

            // 选择默认声音(第一个)
            SelectedVoice = AvailableVoices.FirstOrDefault();

            _logger.LogInformation("成功加载 {Count} 个声音", AvailableVoices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载声音列表失败");
            StatusMessage = $"⚠️ 加载声音失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 预览选中的声音
    /// </summary>
    [RelayCommand]
    private async Task PreviewVoiceAsync()
    {
        if (SelectedVoice == null)
        {
            StatusMessage = "⚠️ 请先选择一个声音";
            return;
        }

        try
        {
            StatusMessage = "🔊 正在生成声音预览...";
            var sampleText = "你好,这是声音预览示例。欢迎使用自动视频生成系统。";

            var audioPath = await _voiceConfigService.PreviewVoiceAsync(
                SelectedVoice.VoiceId,
                sampleText);

            StatusMessage = $"✅ 预览音频已生成: {Path.GetFileName(audioPath)}";

            _logger.LogInformation("预览音频路径: {Path}", audioPath);

            // 播放音频
            StatusMessage = "▶️ 正在播放预览...";
            try
            {
                await _audioPlayerService.PlayAsync(audioPath);
                StatusMessage = "✅ 预览播放完成";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "播放预览失败");
                StatusMessage = $"⚠️ 生成成功但播放失败: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "声音预览失败");
            StatusMessage = $"❌ 预览失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 克隆声音
    /// </summary>
    [RelayCommand]
    private async Task CloneVoiceAsync()
    {
        // TODO: 实现文件选择对话框
        StatusMessage = "🎙️ 声音克隆功能开发中...";
        _logger.LogInformation("声音克隆功能待实现");
        await Task.CompletedTask;
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
                    StatusMessage = "生成成功!";

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
