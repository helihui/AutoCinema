using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro.Pipeline.Configuration;

/// <summary>
/// Pipeline 配置加载器
/// </summary>
public class PipelineConfigurationLoader
{
    private readonly ILogger<PipelineConfigurationLoader> _logger;

    public PipelineConfigurationLoader(ILogger<PipelineConfigurationLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从文件加载配置
    /// </summary>
    public async Task<PipelineConfiguration> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("配置文件不存在,使用默认配置: {FilePath}", filePath);
            return GetDefaultConfiguration();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<PipelineConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                _logger.LogWarning("配置文件解析失败,使用默认配置");
                return GetDefaultConfiguration();
            }

            _logger.LogInformation("已加载 Pipeline 配置: {Name} (v{Version})", config.Name, config.Version);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置文件失败,使用默认配置");
            return GetDefaultConfiguration();
        }
    }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public PipelineConfiguration GetDefaultConfiguration()
    {
        return new PipelineConfiguration
        {
            Name = "默认视频生产流程",
            Version = "1.0",
            Steps = new List<StepConfiguration>
            {
                new() { Name = "StoryboardParsing", DisplayName = "故事板解析", Enabled = true },
                new() { Name = "AssetAggregation", DisplayName = "素材聚合", Enabled = true },
                new() { Name = "SubtitleGeneration", DisplayName = "字幕生成", Enabled = true },
                new() { Name = "VideoComposition", DisplayName = "视频合成", Enabled = true, UseCache = false }
            }
        };
    }
}
