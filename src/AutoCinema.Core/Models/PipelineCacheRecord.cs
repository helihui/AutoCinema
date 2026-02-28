using System;
using System.ComponentModel.DataAnnotations;

namespace AutoCinema.Pro.Models;

/// <summary>
/// Pipeline 缓存记录
/// </summary>
public class PipelineCacheRecord
{
    /// <summary>
    /// 缓存唯一键（如 StepName_Hash）
    /// </summary>
    [Key]
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// 步骤名称
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// 序列化后的缓存 JSON 内容
    /// </summary>
    public string JsonData { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
