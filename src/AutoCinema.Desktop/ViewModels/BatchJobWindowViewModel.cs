using System;
using System.Linq;
using System.Threading.Tasks; // Added
using System.Collections.ObjectModel;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added
using Avalonia; // Added
using Avalonia.Controls.ApplicationLifetimes; // Added
using AutoCinema.Desktop.Views; // Added for SlideshowEditorWindow

namespace AutoCinema.Desktop.ViewModels;

public partial class BatchJobWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<BatchJobWindowViewModel> _logger;
    private System.Timers.Timer? _refreshTimer;

    [ObservableProperty]
    private ObservableCollection<JobItem> _jobs = new();

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public BatchJobWindowViewModel(
        IJobManager jobManager,
        ILogger<BatchJobWindowViewModel> logger)
    {
        _jobManager = jobManager;
        _logger = logger;

        _jobManager.QueueStateChanged += OnQueueStateChanged;

        RefreshJobs();
    }

    private void OnQueueStateChanged(object? sender, EventArgs e)
    {
        RefreshJobs();
    }

    private void RefreshJobs()
    {
        try
        {
            // 获取所有任务 (JobManager 应该提供 GetAllJobs 接口)
            // 暂时我们假设 IJobManager 增加了 GetSnapshot 方法
            // 如果没加，我们需要去加一下

            var jobs = _jobManager.GetSnapshot();

            // 在 UI 线程更新集合
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 智能刷新避免UI闪烁
                var sortedJobs = jobs.OrderByDescending(j => j.CreatedAt).ToList();

                // 移除不存在的
                var currentIds = sortedJobs.Select(j => j.JobId).ToHashSet();
                var toRemove = Jobs.Where(j => !currentIds.Contains(j.JobId)).ToList();
                foreach (var removed in toRemove) { Jobs.Remove(removed); }

                // 添加新增的或者重新排序
                for (int i = 0; i < sortedJobs.Count; i++)
                {
                    var job = sortedJobs[i];
                    int existingIndex = Jobs.IndexOf(job);
                    if (existingIndex == -1)
                    {
                        Jobs.Insert(i, job);
                    }
                    else if (existingIndex != i)
                    {
                        Jobs.Move(existingIndex, i);
                    }
                }

                IsRunning = _jobManager.IsRunning;
                PendingCount = _jobManager.PendingJobCount;

                if (IsRunning)
                    StatusMessage = $"正在运行 - 等待中: {PendingCount}";
                else
                    StatusMessage = "已暂停";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新任务列表失败");
        }
    }

    [RelayCommand]
    private void StartQueue()
    {
        _jobManager.StartProcessing();
        RefreshJobs();
    }

    [RelayCommand]
    private void PauseQueue()
    {
        _jobManager.PauseProcessing();
        RefreshJobs();
    }

    [RelayCommand]
    private void RemoveJob(JobItem job)
    {
        if (job == null) return;
        // 目前 JobManager 只支持 CancelProject，且主要是针对队列中的任务
        _jobManager.CancelJobAsync(job.JobId);
        RefreshJobs();
    }

    [RelayCommand]
    private void OpenResult(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            // 对于 Windows，最好获取绝对路径，避免出现相对路径解析问题导致崩溃
            var fullPath = System.IO.Path.GetFullPath(path);
            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning("尝试打开的文件不存在: {Path}", fullPath);
                return;
            }

            // 使用 explorer.exe /select,path 来定位并在资源管理器中选中文件
            new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{fullPath}\"")
                {
                    UseShellExecute = true
                }
            }.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "无法打开或定位文件: {Path}", path);
        }
    }

    [RelayCommand]
    private void OpenJobCreationWizard()
    {
        try
        {
            var app = (App)Application.Current!;
            var wizardVm = new AutoCinema.Desktop.ViewModels.JobCreation.JobCreationViewModel(app.Services!);
            var wizardWindow = new AutoCinema.Desktop.Views.JobCreationWindow
            {
                DataContext = wizardVm
            };

            // Allow the wizard to open other windows (like SlideshowEditor)
            // Changed from ShowDialog to Show because users want to open multiple times or simultaneously with generation
            // Opened just via Show() without an explicit owner so it doesn't spawn behind the active BatchJobWindow
            wizardWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Job Creation Wizard");
        }
    }

    [RelayCommand]
    private void ReviewStoryboard(JobItem job)
    {
        _logger.LogInformation("================== ReviewStoryboard 触发 ==================");
        if (job == null)
        {
            _logger.LogWarning("ReviewStoryboard 被调用，但 job 参数为 null");
            return;
        }

        _logger.LogInformation("ReviewStoryboard 请求打开任务: JobId={JobId}, Type={Type}, HasScreenplay={HasScreenplay}", 
            job.JobId, job.Type, job.HasScreenplay);

        if (!job.HasScreenplay)
        {
            _logger.LogWarning("任务 {JobId} 的 HasScreenplay 为 false，中止打开剧本窗口", job.JobId);
            return;
        }

        if (job is TextToVideoJob textJob)
        {
            var dataLen = textJob.ProjectData?.ScreenplayRawData?.Length ?? 0;
            _logger.LogInformation("任务 {JobId} 是 TextToVideoJob，其 ProjectData.ScreenplayRawData 长度为: {Length}", job.JobId, dataLen);
        }
        else
        {
            _logger.LogWarning("任务 {JobId} 不是 TextToVideoJob，实际类型为: {Type}", job.JobId, job.GetType().Name);
        }

        try
        {
            var app = (App)Application.Current!;
            var logger = app.Services!.GetRequiredService<ILogger<StoryboardReviewViewModel>>();
            var vm = new StoryboardReviewViewModel(job, _jobManager, logger);

            // 加日志 查看vm的值
            _logger.LogInformation("StoryboardReviewViewModel 创建成功，vm:"+(vm.ScreenplayJsonText).ToString());
            var window = new StoryboardReviewWindow
            {
                DataContext = vm
            };

            // 审阅窗口关闭时刷新任务列表（状态可能已从 WaitingForReview 变为 Processing/Cancelled，或内容已更新）
            window.Closed += (_, _) => RefreshJobs();
            window.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开剧本审阅窗口失败");
        }
    }

    public void Dispose()
    {
        _jobManager.QueueStateChanged -= OnQueueStateChanged;
    }
}
