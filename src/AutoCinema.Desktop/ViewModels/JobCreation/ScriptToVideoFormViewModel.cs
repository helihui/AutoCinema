using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Services;
using AutoCinema.Pro.Services.Actor;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AutoCinema.Desktop.Services;
using AutoCinema.Desktop.Views;
using AutoCinema.Pro.Data;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.EntityFrameworkCore;

namespace AutoCinema.Desktop.ViewModels.JobCreation;

public partial class ScriptToVideoFormViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IVoiceConfigurationService _voiceConfigService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IJobManager _jobManager;
    private readonly ILogger<ScriptToVideoFormViewModel> _logger;
    private readonly IDbContextFactory<CinemaDbContext> _dbContext;
    public event EventHandler? JobCreated;

    [ObservableProperty]
    private string _storyText = string.Empty;

    [ObservableProperty]
    private string _projectTitle = "";

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
        IDbContextFactory<CinemaDbContext> dbContext,
        ILogger<ScriptToVideoFormViewModel> logger)
    {
        _projectService = projectService;
        _voiceConfigService = voiceConfigService;
        _audioPlayerService = audioPlayerService;
        _jobManager = jobManager;
        _dbContext = dbContext;
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
            var safeTitle = string.IsNullOrWhiteSpace(ProjectTitle) ? "未命名任务" : string.Join("_", ProjectTitle.Split(Path.GetInvalidFileNameChars()));
            var project = new VideoProject
            {
                ProjectId = projectId,
                Title = ProjectTitle,
                RawStoryText = StoryText,
                OutputDirectory = Path.Combine(AppContext.BaseDirectory, "output", "gui", safeTitle),
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
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow)
                : null;

            if (topLevel == null)
            {
                StatusMessage = "❌ 无法获取文件选择器上下文";
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择声音样本文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Audio Files")
                    {
                        Patterns = new[] { "*.mp3", "*.wav", "*.m4a" }
                    }
                }
            });

            if (files.Count == 0) return;

            var filePath = files[0].Path.LocalPath;
            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var cloneName = $"{fileName}_克隆";

            StatusMessage = "🎙️ 正在上传并克隆声音...";
            IsProcessing = true;

            var newVoice = await _voiceConfigService.CloneVoiceAsync(cloneName, filePath);

            // Persist the cloned voice to the database
            try
            {
                using var dbContext = await _dbContext.CreateDbContextAsync();
                var record = new AutoCinema.Pro.Models.ClonedVoiceRecord
                {
                    VoiceId = newVoice.VoiceId,
                    DisplayName = newVoice.DisplayName,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.ClonedVoices.Add(record);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("克隆声音已保存到数据库: {VoiceId}", newVoice.VoiceId);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "保存克隆声音到数据库失败");
                // We don't fail the whole cloning process if DB save fails, just log it
            }

            // Add to the list and select it
            AvailableVoices.Add(newVoice);
            SelectedVoice = newVoice;

            StatusMessage = $"✅ 成功克隆声音: {cloneName}";
            _logger.LogInformation("成功克隆声音并添加到列表: {VoiceId}", newVoice.VoiceId);

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            await MessageWindow.ShowAsync(
                "克隆成功",
                $"已成功克隆声音为：{cloneName}\n现在可以在下拉列表中选择它了！",
                mainWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "声音克隆过程中发生错误");
            StatusMessage = $"❌ 克隆失败: {ex.Message}";

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            await MessageWindow.ShowAsync(
                "克隆失败",
                $"克隆声音时遇到错误：\n{ex.Message}",
                mainWindow);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task LoadVoicesAsync()
    {
        try
        {
            var voices = await _voiceConfigService.GetAvailableVoicesAsync();
            AvailableVoices.Clear();
            
            // 1. Load cloned voices from the local database
            try
            {
                using var dbContext = await _dbContext.CreateDbContextAsync();
                var savedClonedVoices = await dbContext.ClonedVoices.ToListAsync();
                foreach (var clonedRecord in savedClonedVoices)
                {
                    AvailableVoices.Add(new VoiceProfile 
                    {
                        VoiceId = clonedRecord.VoiceId,
                        DisplayName = clonedRecord.DisplayName,
                        Description = "用户克隆的声音",
                        Language = "zh-CN",
                        Gender = VoiceGender.Neutral,
                        IsCloned = true,
                        CreatedAt = clonedRecord.CreatedAt
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "从数据库加载克隆声音记录失败");
            }

            // 2. Append official system voices
            foreach (var voice in voices)
            {
                // Check if it's already in the list to avoid duplicates
                if (!AvailableVoices.Any(v => v.VoiceId == voice.VoiceId))
                {
                    AvailableVoices.Add(voice);
                }
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
