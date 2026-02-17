using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Cache;

/// <summary>
/// 基于文件的步骤缓存实现
/// </summary>
/// <typeparam name="TInput">输入类型</typeparam>
/// <typeparam name="TOutput">输出类型</typeparam>
public class FileStepCache<TInput, TOutput> : IStepCache<TInput, TOutput>
{
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;

    public FileStepCache(string cacheDirectory, ILogger logger)
    {
        _cacheDirectory = cacheDirectory;
        _logger = logger;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<TOutput?> GetAsync(string stepName, TInput input, CancellationToken ct)
    {
        var cacheKey = GenerateCacheKey(stepName, input);
        var cachePath = GetCachePath(cacheKey);

        if (!File.Exists(cachePath))
        {
            _logger.LogDebug(
                "缓存未命中: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);
            return default;
        }

        try
        {
            var json = await File.ReadAllTextAsync(cachePath, ct);
            var result = JsonSerializer.Deserialize<TOutput>(json);

            _logger.LogInformation(
                "缓存命中: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "读取缓存失败: StepName={StepName}",
                stepName);
            return default;
        }
    }

    public async Task SetAsync(string stepName, TInput input, TOutput output, CancellationToken ct)
    {
        var cacheKey = GenerateCacheKey(stepName, input);
        var cachePath = GetCachePath(cacheKey);

        try
        {
            var json = JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(cachePath, json, ct);

            _logger.LogDebug(
                "缓存已保存: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "保存缓存失败: StepName={StepName}",
                stepName);
        }
    }

    public Task ClearAsync(string? stepName = null, CancellationToken ct = default)
    {
        try
        {
            if (stepName == null)
            {
                // 清除所有缓存
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, recursive: true);
                    Directory.CreateDirectory(_cacheDirectory);
                }
                _logger.LogInformation("已清除所有缓存");
            }
            else
            {
                // 清除特定步骤的缓存
                var pattern = $"{stepName}_*.json";
                var files = Directory.GetFiles(_cacheDirectory, pattern);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                _logger.LogInformation(
                    "已清除缓存: StepName={StepName}, FileCount={FileCount}",
                    stepName, files.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清除缓存失败");
        }

        return Task.CompletedTask;
    }

    private string GenerateCacheKey(string stepName, TInput input)
    {
        // 序列化输入为 JSON
        var json = JsonSerializer.Serialize(input);

        // 计算 SHA256 哈希
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hashString = Convert.ToHexString(hash)[..16]; // 取前16个字符

        return $"{stepName}_{hashString}";
    }

    private string GetCachePath(string cacheKey)
    {
        return Path.Combine(_cacheDirectory, $"{cacheKey}.json");
    }
}
