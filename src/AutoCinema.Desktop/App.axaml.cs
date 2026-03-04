using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoCinema.Pro.Configuration;
using AutoCinema.Pro.Data;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline;
using AutoCinema.Pro.Pipeline.Configuration;
using AutoCinema.Pro.Pipeline.Steps;
using AutoCinema.Pro.Services;
using AutoCinema.Pro.Services.Actor;
using AutoCinema.Pro.Services.Director;
using AutoCinema.Pro.Services.Editor;
using AutoCinema.Pro.Services.Audio;
using AutoCinema.Pro.Services.Screenplay;
using AutoCinema.Core.Services.Jobs;
using AutoCinema.Desktop.Services;
using AutoCinema.Desktop.ViewModels;

using AutoCinema.Desktop.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AutoCinema.Desktop;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "app.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("========================================");
        Log.Information("AutoCinema.Desktop 启动中...");
        Log.Information("应用程序目录: {BaseDirectory}", AppContext.BaseDirectory);
        Log.Information("========================================");

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 配置 FFmpeg 路径
            ConfigureFFmpeg(configuration);

            var serviceCollection = new ServiceCollection();

            ConfigureServices(serviceCollection, configuration);
            Services = serviceCollection.BuildServiceProvider();

            Log.Information("依赖注入容器已配置完成");

            // 确保数据库已创建/迁移
            using (var scope = Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
                dbContext.Database.Migrate();
                Log.Information("数据库已初始化(Migrate)");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var viewModel = Services.GetRequiredService<MainHomeWindowViewModel>();
                desktop.MainWindow = new MainHomeWindow
                {
                    DataContext = viewModel
                };
                Log.Information("主窗口(MainHomeWindow)已创建");
            }

            Log.Information("应用程序启动成功！");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用程序启动失败");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup_error.log"),
                $"[{DateTime.Now}] 启动崩溃: {ex}\n堆栈: {ex.StackTrace}");
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions();

        // 注册 Serilog 日志
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });


        // 注册配置
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<VolcengineOptions>(configuration.GetSection(VolcengineOptions.SectionName));
        services.Configure<MiniMaxOptions>(configuration.GetSection(MiniMaxOptions.SectionName));
        services.Configure<PipelineOptions>(configuration.GetSection(PipelineOptions.SectionName));

        // 注册数据库
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? @"Data Source=e:\100.Work\NestCoreProject\AutoCinema\src\db\autocinema.db";
        services.AddPooledDbContextFactory<CinemaDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddDbContext<CinemaDbContext>(options =>
            options.UseSqlite(connectionString));

        // 注册核心服务
        services.AddHttpClient<VolcengineLlmService>();
        services.AddHttpClient<IImageGenerationService, VolcengineImageService>();
        services.AddHttpClient<ISpeechGenerationService, MiniMaxSpeechService>();
        services.AddHttpClient<IVoiceConfigurationService, MiniMaxVoiceConfigurationService>();
        services.AddSingleton<IAudioPlayerService, NAudioPlayerService>();
        services.AddSingleton<IAudioPreprocessorService, FFMpegAudioPreprocessorService>();

        services.AddSingleton<IStoryboardService, StoryboardService>();
        services.AddSingleton<IAudioAnalysisService, NAudioAnalysisService>();
        services.AddSingleton<ISubtitleService, SrtSubtitleService>();
        services.AddSingleton<IVideoCompositionService, FFMpegVideoService>();

        // 注册 Pipeline 步骤
        services.AddSingleton<StoryboardParsingStep>();
        services.AddSingleton<AssetAggregationStep>();
        services.AddSingleton<SubtitleGenerationStep>();
        services.AddSingleton<VideoCompositionStep>();
        // 剧本生成服务（全量剧本模式）
        services.AddSingleton<ScreenplayGeneratorService>();
        services.AddSingleton<SubtitleExporterService>();
        services.AddSingleton<TimelineExporterService>();
        services.AddSingleton<ScreenplayGenerationStep>();

        // 注册 Pipeline 配置
        services.AddSingleton<PipelineConfigurationLoader>();

        services.AddSingleton<IVideoProductionPipeline, VideoProductionPipeline>();

        // 注册 Job Handlers
        services.AddSingleton<IJobHandler, TextToVideoJobHandler>();
        services.AddSingleton<IJobHandler, SlideshowJobHandler>();

        // 注册 JobManager (它依赖 IEnumerable<IJobHandler>)
        services.AddSingleton<IJobManager, JobManager>();

        // 注册 ViewModels
        services.AddTransient<SlideshowEditorViewModel>();
        // ScriptToVideoFormViewModel and JobCreationViewModel will be created manually or via factory to handle events/callbacks
        // But for pure DI resolution without callbacks, we can register them if constructors are clean.
        // Let's keep them registered for now, but we'll likely instantiate JobCreationViewModel manually in the Window.


        services.AddSingleton<MainHomeWindowViewModel>();
        services.AddSingleton<BatchJobWindowViewModel>();
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
            FFMpegCore.GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegDir);
            Log.Information("使用 FFmpeg 路径: {FFmpegDir}", ffmpegDir);
        }
        else
        {
            Log.Warning("未找到 FFmpeg 目录，将使用系统 PATH 中的 FFmpeg");
        }
    }
}
