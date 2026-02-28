namespace AutoCinema.Pro.Models.Audio;

/// <summary>
/// 音频预处理结果
/// </summary>
public class AudioProcessResult
{
    /// <summary>处理后输出的临时文件路径</summary>
    public string ProcessedFilePath { get; set; } = string.Empty;

    /// <summary>处理后的实际时长</summary>
    public TimeSpan ActualDuration { get; set; }

    /// <summary>处理后的实际文件大小（字节）</summary>
    public long FileSizeBytes { get; set; }
}
