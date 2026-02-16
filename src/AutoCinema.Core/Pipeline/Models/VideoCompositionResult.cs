namespace AutoCinema.Pro.Pipeline.Models;

/// <summary>
/// 视频合成结果
/// </summary>
public class VideoCompositionResult
{
    /// <summary>
    /// 输出视频路径
    /// </summary>
    public required string OutputPath { get; set; }

    /// <summary>
    /// 文件大小(字节)
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 视频时长
    /// </summary>
    public TimeSpan Duration { get; set; }
}
