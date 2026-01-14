using Polly;
using Polly.Extensions.Http;

namespace AutoCinema.Pro.Infrastructure.Resilience;

/// <summary>
/// Polly 重试策略定义
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// 图片生成重试策略：指数退避，最多重试 3 次
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetImageGenerationPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429) // Rate limit
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    Console.WriteLine($"[图片生成] 重试 {attempt}/3，等待 {delay.TotalSeconds:F1}s，原因: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                });
    }

    /// <summary>
    /// 语音合成重试策略
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetSpeechGenerationPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429) // Rate limit
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    Console.WriteLine($"[语音合成] 重试 {attempt}/3，等待 {delay.TotalSeconds:F1}s，原因: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                });
    }

    /// <summary>
    /// 通用 API 调用重试策略
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetGeneralApiPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(attempt));
    }
}
