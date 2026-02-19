using System;
using System.Linq;
using System.Collections.ObjectModel;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Desktop.ViewModels;

public partial class BatchJobWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IJobManager _jobManager;
    private readonly IProjectService _projectService;
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
        IProjectService projectService,
        ILogger<BatchJobWindowViewModel> logger)
    {
        _jobManager = jobManager;
        _projectService = projectService;
        _logger = logger;

        _jobManager.QueueStateChanged += OnQueueStateChanged;

        // 启动定时刷新以获取进度更新
        _refreshTimer = new System.Timers.Timer(1000);
        _refreshTimer.Elapsed += (s, e) => RefreshJobs();
        _refreshTimer.Start();

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
                // 简单全量刷新 (实际应该做 diff)
                Jobs.Clear();
                foreach (var job in jobs.OrderByDescending(j => j.CreatedAt))
                {
                    Jobs.Add(job);
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
    private void OpenResultCommand(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;

        try
        {
            new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true
                }
            }.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "无法打开文件: {Path}", path);
        }
    }

    public void Dispose()
    {
        _jobManager.QueueStateChanged -= OnQueueStateChanged;
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }
}
