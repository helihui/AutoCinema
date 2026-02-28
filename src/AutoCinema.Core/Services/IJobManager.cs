using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;

namespace AutoCinema.Pro.Services;

/// <summary>
/// 任务队列管理接口
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// 将项目添加到处理队列
    /// </summary>
    Task EnqueueAsync(JobItem job);

    /// <summary>
    /// 开始处理队列
    /// </summary>
    void StartProcessing();

    /// <summary>
    /// 暂停处理
    /// </summary>
    void PauseProcessing();

    /// <summary>
    /// 取消特定任务
    /// </summary>
    Task CancelJobAsync(string jobId);

    /// <summary>
    /// 队列状态发生变化时触发
    /// </summary>
    event EventHandler? QueueStateChanged;

    /// <summary>
    /// 获取队列中等待的任务数
    /// </summary>
    int PendingJobCount { get; }

    /// <summary>
    /// 更新特定的任务并持久化
    /// </summary>
    Task UpdateJobAsync(JobItem job);

    /// <summary>
    /// 获取所有任务的快照
    /// </summary>
    IEnumerable<JobItem> GetSnapshot();

    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }
}
