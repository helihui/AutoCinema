using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Services.Actor;
using AutoCinema.Pro.Services.Director;
using AutoCinema.Pro.Services.Editor;
using FFMpegCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoCinema.Pro;

/// <summary>
/// AutoCinema.Pro 主程序入口
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          AutoCinema.Pro - 自动化视频生成系统           ║");
        Console.WriteLine("║                     Director-Actor-Editor Model        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var builder = Host.CreateApplicationBuilder(args);

        // 配置 FFmpeg 路径（使用项目内的 FFmpeg，优先读取配置文件）
        ConfigureFFmpeg(builder.Configuration);

        // 配置日志
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // 配置绑定
        ConfigureOptions(builder);

        // 注册服务
        ConfigureServices(builder);

        var host = builder.Build();

        // 运行示例
        await RunExampleAsync(host);
    }

    /// <summary>
    /// 配置 FFmpeg 使用项目内的可执行文件
    /// </summary>
    private static void ConfigureFFmpeg(IConfiguration? configuration = null)
    {
        var baseDir = AppContext.BaseDirectory;
        string? ffmpegDir = null;

        // 优先使用配置文件中的路径
        var configuredPath = configuration?.GetSection("Pipeline:FFmpegDirectory").Value;
        if (!string.IsNullOrEmpty(configuredPath))
        {
            if (Path.IsPathRooted(configuredPath))
            {
                ffmpegDir = configuredPath;
            }
            else
            {
                ffmpegDir = Path.GetFullPath(Path.Combine(baseDir, configuredPath));
            }
        }

        // 如果配置的路径不存在，尝试自动查找
        if (string.IsNullOrEmpty(ffmpegDir) || !Directory.Exists(ffmpegDir))
        {
            // FFmpeg 位于 src/ffmpeg 目录，相对于运行时位置需要回溯
            // 运行时位置: src/AutoCinema.Pro/bin/Debug/net8.0/
            // FFmpeg 位置: src/ffmpeg/
            ffmpegDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "ffmpeg"));
        }

        // 如果找不到，尝试从项目根目录查找
        if (!Directory.Exists(ffmpegDir))
        {
            // 可能是发布后运行，尝试其他路径
            ffmpegDir = Path.GetFullPath(Path.Combine(baseDir, "ffmpeg"));
        }

        if (!Directory.Exists(ffmpegDir))
        {
            Console.WriteLine($"[警告] 未找到 FFmpeg 目录，将使用系统 PATH 中的 FFmpeg");
            return;
        }

        var ffmpegPath = Path.Combine(ffmpegDir, "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
        {
            Console.WriteLine($"[警告] 未找到 ffmpeg.exe，将使用系统 PATH 中的 FFmpeg");
            return;
        }

        Console.WriteLine($"[信息] 使用项目内 FFmpeg: {ffmpegDir}");

        // 配置 FFMpegCore 全局选项
        GlobalFFOptions.Configure(options =>
        {
            options.BinaryFolder = ffmpegDir;
        });
    }

    private static void ConfigureOptions(HostApplicationBuilder builder)
    {
        builder.Services.Configure<LlmOptions>(
            builder.Configuration.GetSection(LlmOptions.SectionName));
        builder.Services.Configure<VolcengineOptions>(
            builder.Configuration.GetSection(VolcengineOptions.SectionName));
        builder.Services.Configure<MiniMaxOptions>(
            builder.Configuration.GetSection(MiniMaxOptions.SectionName));
        builder.Services.Configure<PipelineOptions>(
            builder.Configuration.GetSection(PipelineOptions.SectionName));
    }

    private static void ConfigureServices(HostApplicationBuilder builder)
    {
        // 注册火山引擎 LLM 服务
        builder.Services.AddHttpClient<VolcengineLlmService>();

        // 注册 HttpClient
        builder.Services.AddHttpClient<IImageGenerationService, VolcengineImageService>();
        builder.Services.AddHttpClient<ISpeechGenerationService, MiniMaxSpeechService>();

        // 注册导演层服务
        builder.Services.AddSingleton<IStoryboardService, StoryboardService>();

        // 注册剪辑层服务
        builder.Services.AddSingleton<IAudioAnalysisService, NAudioAnalysisService>();
        builder.Services.AddSingleton<ISubtitleService, SrtSubtitleService>();
        builder.Services.AddSingleton<IVideoCompositionService, FFMpegVideoService>();

        // 注册流水线
        builder.Services.AddSingleton<IVideoProductionPipeline, VideoProductionPipeline>();
    }

    private static async Task RunExampleAsync(IHost host)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        // 检查配置
        var llmOptions = host.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
        if (llmOptions.ApiKey.StartsWith("<YOUR_"))
        {
            logger.LogWarning("====================================");
            logger.LogWarning("请先配置 appsettings.json 中的 API 密钥！");
            logger.LogWarning("====================================");
            Console.WriteLine();
            Console.WriteLine("使用方法:");
            Console.WriteLine("1. 编辑 appsettings.json，填入真实的 API 密钥：");
            Console.WriteLine("   - Llm.ApiKey: OpenAI 或兼容 API 的密钥");
            Console.WriteLine("   - Volcengine.ApiKey: 火山引擎 Ark API 密钥");
            Console.WriteLine("   - MiniMax.ApiKey 和 GroupId: MiniMax TTS 凭据");
            Console.WriteLine();
            Console.WriteLine("2. 确保系统已安装 FFmpeg 并添加到 PATH");
            Console.WriteLine();
            Console.WriteLine("3. 重新运行程序");
            return;
        }

        // 读取 Pipeline 配置
        var pipelineOptions = host.Services.GetRequiredService<IOptions<PipelineOptions>>().Value;

        // 创建示例项目
        var project = new VideoProject
        {
            ProjectId = Guid.NewGuid().ToString("N")[..8],
            Title = pipelineOptions.DemoProject.Title,
            OutputDirectory = "./output/demo",
            RawStoryText = pipelineOptions.DemoProject.StoryText,
            BaseVisualStyle = pipelineOptions.DefaultVisualStyle
        };

        Console.WriteLine($"项目配置:");
        Console.WriteLine($"  标题: {project.Title}");
        Console.WriteLine($"  视觉风格: {project.BaseVisualStyle}");
        Console.WriteLine($"  输出目录: {project.OutputDirectory}");
        Console.WriteLine();
        Console.Write("按 Enter 键开始生成，或按 Ctrl+C 取消...");
        Console.ReadLine();
        Console.WriteLine();

        try
        {
            var pipeline = host.Services.GetRequiredService<IVideoProductionPipeline>();

            // 进度报告
            var progress = new Progress<ProductionProgress>(p =>
            {
                Console.WriteLine($"[{p.Percentage,3}%] {p.Stage} - {p.Step}");
            });

            var outputPath = await pipeline.ProduceAsync(project, progress);

            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    视频生成完成！                      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine($"输出文件: {Path.GetFullPath(outputPath)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "视频生成失败");
            Console.WriteLine();
            Console.WriteLine($"错误: {ex.Message}");
        }
    }
}
