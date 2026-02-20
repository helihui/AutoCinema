using System.Text.Json.Serialization;
using AutoCinema.Pro.Pipeline;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoCinema.Pro.Models.Jobs;

/// <summary>
/// 任务状态
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// 通用任务基类
/// </summary>
[JsonDerivedType(typeof(TextToVideoJob), typeDiscriminator: "TextOnVideo")]
[JsonDerivedType(typeof(SlideshowJob), typeDiscriminator: "ImageToVideo")]
public abstract class JobItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// 关联的外部ID (如 ProjectId)
    /// </summary>
    public string? CorrelationId { get; set; }
    public JobType Type { get; set; }

    private JobStatus _status = JobStatus.Pending;
    public JobStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    private DateTime? _finishedAt;
    public DateTime? FinishedAt
    {
        get => _finishedAt;
        set => SetProperty(ref _finishedAt, value);
    }

    public string Title { get; set; } = string.Empty;

    private string? _outputPath;
    public string? OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private ProductionProgress _progress = new() { Stage = "Pending", Step = "Waiting to start", Percentage = 0 };
    [JsonIgnore]
    public ProductionProgress Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }
}
