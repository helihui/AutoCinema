namespace AutoCinema.Pro.Models;

/// <summary>
/// 任务类型
/// </summary>
public enum JobType
{
    /// <summary>
    /// 剧本生成视频 (Text-to-Video)
    /// </summary>
    TextOnVideo,

    /// <summary>
    /// 图片生成视频 (Image-to-Video)
    /// </summary>
    ImageToVideo,

    /// <summary>
    /// 视频裁切/缩放 (Video Resizing)
    /// </summary>
    VideoResizing,

    /// <summary>
    /// 视频配音 (Audio Dubbing)
    /// </summary>
    AudioDubbing,

    // 兼容旧名字 (如果有序列化数据可能需要注意，但目前只是枚举)
    ScriptToVideo = TextOnVideo
}
