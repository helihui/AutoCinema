using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Data;
using AutoCinema.Pro.Endpoints;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Pipeline.Configuration;
using AutoCinema.Pro.Pipeline.Steps;
using AutoCinema.Pro.Services;
using AutoCinema.Pro.Services.Actor;
using AutoCinema.Pro.Services.Director;
using AutoCinema.Pro.Services.Editor;
using FFMpegCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoCinema.Pro;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 配置 FFmpeg 路径
        ConfigureFFmpeg(builder.Configuration);

        // 配置日志
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // 注册核心服务
        ConfigureOptions(builder.Services, builder.Configuration);
        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();

        // 确保数据库已创建
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            dbContext.Database.EnsureCreated();
        }

        // 映射 API 接口
        app.MapVideoEndpoints();

        // 默认健康检查或首页
        app.MapGet("/", () => "AutoCinema.Pro API is running.");

        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          AutoCinema.Pro - 自动化视频生成 API           ║");
        Console.WriteLine("║                     Director-Actor-Editor Model        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝");

        app.Run();
    }

    private static void ConfigureFFmpeg(IConfiguration configuration)
    {
        var baseDir = AppContext.BaseDirectory;
        string? ffmpegDir = null;

        var configuredPath = configuration.GetSection("Pipeline:FFmpegDirectory").Value;
        if (!string.IsNullOrEmpty(configuredPath))
        {
            ffmpegDir = Path.IsPathRooted(configuredPath) ? configuredPath : Path.GetFullPath(Path.Combine(baseDir, configuredPath));
        }

        if (string.IsNullOrEmpty(ffmpegDir) || !Directory.Exists(ffmpegDir))
        {
            ffmpegDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "ffmpeg"));
        }

        if (!Directory.Exists(ffmpegDir))
        {
            ffmpegDir = Path.GetFullPath(Path.Combine(baseDir, "ffmpeg"));
        }

        if (Directory.Exists(ffmpegDir))
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegDir);
            Console.WriteLine($"[信息] 使用项目内 FFmpeg: {ffmpegDir}");
        }
        else
        {
            Console.WriteLine($"[警告] 未找到 FFmpeg 目录，将使用系统 PATH 中的 FFmpeg");
        }
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<VolcengineOptions>(configuration.GetSection(VolcengineOptions.SectionName));
        services.Configure<MiniMaxOptions>(configuration.GetSection(MiniMaxOptions.SectionName));
        services.Configure<PipelineOptions>(configuration.GetSection(PipelineOptions.SectionName));
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 注册数据库服务
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=autocinema.db";
        services.AddPooledDbContextFactory<CinemaDbContext>(options =>
            options.UseSqlite(connectionString));

        // 注册 Scoped DbContext 供启动时的 EnsureCreated 使用
        services.AddDbContext<CinemaDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddHttpClient<VolcengineLlmService>();
        services.AddHttpClient<IImageGenerationService, VolcengineImageService>();
        services.AddHttpClient<ISpeechGenerationService, MiniMaxSpeechService>();

        services.AddSingleton<IStoryboardService, StoryboardService>();
        services.AddSingleton<IAudioAnalysisService, NAudioAnalysisService>();
        services.AddSingleton<ISubtitleService, SrtSubtitleService>();
        services.AddSingleton<IVideoCompositionService, FFMpegVideoService>();

        // 注册 Pipeline 步骤
        services.AddScoped<StoryboardParsingStep>();
        services.AddScoped<AssetAggregationStep>();
        services.AddScoped<SubtitleGenerationStep>();
        services.AddScoped<VideoCompositionStep>();

        // 注册 Pipeline 配置
        services.AddSingleton<PipelineConfigurationLoader>();

        // 注册 Pipeline
        services.AddScoped<VideoProductionPipeline>();

        // 注册新增加的项目管理服务
        services.AddSingleton<IProjectService, ProjectService>();
    }
}
