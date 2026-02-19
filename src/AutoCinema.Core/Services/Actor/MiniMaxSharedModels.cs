namespace AutoCinema.Pro.Services.Actor;

/// <summary>
/// MiniMax API 共享响应模型
/// </summary>
internal class MiniMaxBaseResp
{
    public int StatusCode { get; set; }
    public string? StatusMsg { get; set; }
}

internal class MiniMaxAudioData
{
    public string? Audio { get; set; }
}
