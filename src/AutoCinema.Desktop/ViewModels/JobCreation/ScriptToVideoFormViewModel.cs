using System;
using System.Collections.ObjectModel;
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

namespace AutoCinema.Desktop.ViewModels.JobCreation;

public partial class ScriptToVideoFormViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IVoiceConfigurationService _voiceConfigService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IJobManager _jobManager;
    private readonly ILogger<ScriptToVideoFormViewModel> _logger;
    public event EventHandler? JobCreated;

    [ObservableProperty]
    private string _storyText = string.Empty;

    [ObservableProperty]
    private string _projectTitle = "新视频项目";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _visualStyle = "Cinematic, high quality, detailed, professional lighting";

    [ObservableProperty]
    private string _characterPrompt = "";

    // Voice Configuration
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

    [ObservableProperty]
    private bool _supportsVoiceCloning;

    public System.Collections.Generic.List<string> Emotions { get; } = new()
    {
        "neutral",
        "happy",
        "sad",
        "angry",
        "surprised"
    };

    public ScriptToVideoFormViewModel(
        IProjectService projectService,
        IVoiceConfigurationService voiceConfigService,
        IAudioPlayerService audioPlayerService,
        IJobManager jobManager,
        ILogger<ScriptToVideoFormViewModel> logger)
    {
        _projectService = projectService;
        _voiceConfigService = voiceConfigService;
        _audioPlayerService = audioPlayerService;
        _jobManager = jobManager;
        _logger = logger;

        _supportsVoiceCloning = _voiceConfigService?.SupportsVoiceCloning ?? false;
        _ = LoadVoicesAsync();
    }

    [RelayCommand]
    private async Task CreateJobAsync()
    {
        if (string.IsNullOrWhiteSpace(StoryText))
        {
            StatusMessage = "⚠️ 请先输入您的创意故事!";
            return;
        }

        IsProcessing = true;
        StatusMessage = "🚀 正在创建任务...";

        try
        {
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

            await _projectService.SubmitProjectAsync(project);

            var job = new TextToVideoJob
            {
                ProjectData = project,
                Title = project.Title,
                CorrelationId = project.ProjectId
            };

            await _jobManager.EnqueueAsync(job);

            _logger.LogInformation("任务已创建: {JobId}", job.JobId);
            StatusMessage = "✅ 任务已创建";
            JobCreated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建任务失败");
            StatusMessage = $"❌ 错误: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task CloneVoiceAsync()
    {
        // TODO: 实现文件选择对话框
        StatusMessage = "🎙️ 声音克隆功能开发中...";
        _logger.LogInformation("声音克隆功能待实现");
        await Task.CompletedTask;
    }

    private async Task LoadVoicesAsync()
    {
        try
        {
            var voices = await _voiceConfigService.GetAvailableVoicesAsync();
            AvailableVoices.Clear();
            foreach (var voice in voices)
            {
                AvailableVoices.Add(voice);
            }
            SelectedVoice = AvailableVoices.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载声音列表失败");
        }
    }

    // ... (Add PreviewVoice logic if needed in the wizard, maybe simplified)
    [RelayCommand]
    private async Task PreviewVoiceAsync()
    {
        if (SelectedVoice == null) return;
        try
        {
            StatusMessage = "🔊 生成预览...";
            var audioPath = await _voiceConfigService.PreviewVoiceAsync(SelectedVoice.VoiceId, "声音预览测试");
            await _audioPlayerService.PlayAsync(audioPath);
            StatusMessage = "✅ 预览播放完毕";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 预览失败: {ex.Message}";
        }
    }
}
