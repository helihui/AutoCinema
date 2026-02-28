using AutoCinema.Pro.Models.Screenplay;

namespace AutoCinema.Pro.Pipeline.Models;

/// <summary>
/// 分镜审阅关卡（CoDesign 模式）
/// 
/// 当 codesign=true 时，剧本生成完成后 Pipeline 通过此对象挂起，
/// 等待用户在 UI 审阅/修改剧本后调用 Approve() 解锁并继续执行。
/// 
/// 实现原理：TaskCompletionSource 让 pipeline 线程异步等待，
/// 不阻塞 UI 线程。
/// </summary>
public class StoryboardReviewGate
{
    private readonly TaskCompletionSource<ScreenplayDocument> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>等待审阅的原始剧本（LLM 生成，未经用户修改）</summary>
    public ScreenplayDocument PendingScreenplay { get; }

    public StoryboardReviewGate(ScreenplayDocument pendingScreenplay)
    {
        PendingScreenplay = pendingScreenplay;
    }

    /// <summary>
    /// Pipeline 调用此方法挂起，直到用户确认或取消。
    /// </summary>
    public Task<ScreenplayDocument> WaitForApprovalAsync(CancellationToken ct = default)
        => _tcs.Task.WaitAsync(ct);

    /// <summary>
    /// UI 层调用：用户已审阅/修改剧本，点击「确认生成」后调用此方法解锁 pipeline。
    /// </summary>
    /// <param name="approvedScreenplay">用户修改后的剧本（或原稿）</param>
    public void Approve(ScreenplayDocument approvedScreenplay)
        => _tcs.TrySetResult(approvedScreenplay);

    /// <summary>
    /// UI 层调用：用户取消，pipeline 将收到 OperationCanceledException。
    /// </summary>
    public void Cancel()
        => _tcs.TrySetCanceled();

    /// <summary>是否已解锁（已确认或已取消）</summary>
    public bool IsCompleted => _tcs.Task.IsCompleted;
}
