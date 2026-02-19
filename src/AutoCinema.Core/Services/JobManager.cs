using System.Collections.Concurrent;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Core.Services.Jobs;
using AutoCinema.Pro.Pipeline;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Services;

public class JobManager : IJobManager, IDisposable
{
    private readonly ConcurrentQueue<JobItem> _queue = new();
    // 增加 allJobs 用于跟踪所有任务状态 (包括已完成的)
    private readonly ConcurrentDictionary<string, JobItem> _allJobs = new();

    private readonly IEnumerable<IJobHandler> _handlers;
    private readonly ILogger<JobManager> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _processingTask;
    private bool _isRunning;

    public event EventHandler? QueueStateChanged;

    public int PendingJobCount => _queue.Count;
    public bool IsRunning => _isRunning;

    public JobManager(
        IEnumerable<IJobHandler> handlers,
        ILogger<JobManager> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有任务的快照
    /// </summary>
    public IEnumerable<JobItem> GetSnapshot()
    {
        return _allJobs.Values.ToArray();
    }

    public Task EnqueueAsync(JobItem job)
    {
        _queue.Enqueue(job);
        _allJobs.TryAdd(job.JobId, job); // 记录到全局字典
        _logger.LogInformation("任务 {JobId} ({Type}) 已加入队列", job.JobId, job.Type);
        QueueStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void StartProcessing()
    {
        if (_isRunning) return;
        _isRunning = true;

        if (_processingTask == null || _processingTask.IsCompleted)
        {
            _processingTask = Task.Run(ProcessQueueLoop);
        }

        _logger.LogInformation("任务处理队列已启动");
        QueueStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PauseProcessing()
    {
        _isRunning = false;
        _logger.LogInformation("任务处理队列已暂停");
        QueueStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task CancelJobAsync(string jobId)
    {
        // 简单实现：移除队列中的特定任务
        // 注意：这不会停止正在运行的任务（需要更复杂的 Token 管理）

        var items = _queue.ToArray();
        _queue.Clear();

        foreach (var item in items)
        {
            // 支持通过 JobId 或 CorrelationId (ProjectId) 取消
            if (item.JobId != jobId && item.CorrelationId != jobId)
            {
                _queue.Enqueue(item);
            }
        }

        QueueStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private async Task ProcessQueueLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            if (!_isRunning)
            {
                await Task.Delay(1000);
                continue;
            }

            if (_queue.TryDequeue(out var job))
            {
                try
                {
                    _logger.LogInformation("开始处理任务: {JobId} ({Type})", job.JobId, job.Type);

                    var handler = _handlers.FirstOrDefault(h => h.SupportedType == job.Type);
                    if (handler == null)
                    {
                        var msg = $"未找到处理类型为 {job.Type} 的处理器";
                        _logger.LogError(msg);
                        job.Status = JobStatus.Failed;
                        job.ErrorMessage = msg;
                        continue;
                    }

                    job.Status = JobStatus.Processing;
                    QueueStateChanged?.Invoke(this, EventArgs.Empty);

                    var progress = new Progress<ProductionProgress>(p =>
                    {
                        job.Progress = p;
                        // 这里可以选择是否频繁触发状态变更事件，或者由 UI 主动轮询
                        // QueueStateChanged?.Invoke(this, EventArgs.Empty);
                    });

                    await handler.ExecuteAsync(job, progress, _cts.Token);

                    job.Status = JobStatus.Completed;
                    job.FinishedAt = DateTime.Now;
                    _logger.LogInformation("任务处理完成: {JobId}", job.JobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理任务失败: {JobId}", job.JobId);
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                }
                finally
                {
                    QueueStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                await Task.Delay(1000); // 队列为空时等待
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
