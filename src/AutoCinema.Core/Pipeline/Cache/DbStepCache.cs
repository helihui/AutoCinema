using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoCinema.Pro.Data;
using AutoCinema.Pro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Cache;

/// <summary>
/// 基于 SQLite 数据库的步骤缓存实现
/// </summary>
/// <typeparam name="TInput">输入类型</typeparam>
/// <typeparam name="TOutput">输出类型</typeparam>
public class DbStepCache<TInput, TOutput> : IStepCache<TInput, TOutput>
{
    private readonly IDbContextFactory<CinemaDbContext> _dbContextFactory;
    private readonly ILogger _logger;

    public DbStepCache(IDbContextFactory<CinemaDbContext> dbContextFactory, ILogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<TOutput?> GetAsync(string stepName, TInput input, CancellationToken ct)
    {
        var cacheKey = GenerateCacheKey(stepName, input);

        try
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var record = await db.PipelineCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CacheKey == cacheKey, ct);

            if (record == null)
            {
                _logger.LogDebug(
                    "数据库缓存未命中: StepName={StepName}, CacheKey={CacheKey}",
                    stepName, cacheKey);
                return default;
            }

            var result = JsonSerializer.Deserialize<TOutput>(record.JsonData);
            
            _logger.LogInformation(
                "数据库缓存命中: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "读取数据库缓存失败: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);
            return default;
        }
    }

    public async Task SetAsync(string stepName, TInput input, TOutput output, CancellationToken ct)
    {
        var cacheKey = GenerateCacheKey(stepName, input);

        try
        {
            var json = JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = false // 数据库存储不需缩进，节省空间
            });

            using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            
            var existingRecord = await db.PipelineCaches.FirstOrDefaultAsync(c => c.CacheKey == cacheKey, ct);
            if (existingRecord != null)
            {
                // 如果已存在，更新内容
                existingRecord.JsonData = json;
                existingRecord.CreatedAt = DateTime.UtcNow;
                db.PipelineCaches.Update(existingRecord);
            }
            else
            {
                // 新增缓存记录
                db.PipelineCaches.Add(new PipelineCacheRecord
                {
                    CacheKey = cacheKey,
                    StepName = stepName,
                    JsonData = json,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync(ct);

            _logger.LogDebug(
                "缓存已保存至数据库: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "保存缓存至数据库失败: StepName={StepName}, CacheKey={CacheKey}",
                stepName, cacheKey);
        }
    }

    public async Task ClearAsync(string? stepName = null, CancellationToken ct = default)
    {
        try
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            if (stepName == null)
            {
                // 清除所有缓存
                await db.PipelineCaches.ExecuteDeleteAsync(ct);
                _logger.LogInformation("已清除数据库中所有 Pipeline 缓存");
            }
            else
            {
                // 清除特定步骤的缓存
                var count = await db.PipelineCaches
                    .Where(c => c.StepName == stepName)
                    .ExecuteDeleteAsync(ct);

                _logger.LogInformation(
                    "已清除数据库缓存: StepName={StepName}, DeletedCount={Count}",
                    stepName, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清除数据库缓存失败");
        }
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
}
