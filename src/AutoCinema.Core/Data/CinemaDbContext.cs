using System.Text.Json;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace AutoCinema.Pro.Data;

public class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options) : base(options)
    {
    }

    public DbSet<AutoCinema.Pro.Models.Jobs.JobItem> Jobs { get; set; } = null!;
    public DbSet<ClonedVoiceRecord> ClonedVoices { get; set; } = null!;
    public DbSet<PipelineCacheRecord> PipelineCaches { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 PipelineCacheRecord 主键
        modelBuilder.Entity<PipelineCacheRecord>()
            .HasKey(c => c.CacheKey);



        // 配置 ClonedVoiceRecord 主键
        modelBuilder.Entity<ClonedVoiceRecord>()
            .HasKey(c => c.VoiceId);

        // 配置 JobItem 及其派生类 (Table-Per-Hierarchy)
        modelBuilder.Entity<JobItem>()
            .HasKey(j => j.JobId);

        modelBuilder.Entity<JobItem>()
            .HasDiscriminator(j => j.Type)
            .HasValue<TextToVideoJob>(JobType.TextOnVideo)
            .HasValue<SlideshowJob>(JobType.ImageToVideo);

        modelBuilder.Entity<JobItem>()
            .Property(j => j.Progress)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<ProductionProgress>(v, (JsonSerializerOptions?)null)
            );

        // SlideshowJob 的 Items 按 JSON 存储
        modelBuilder.Entity<SlideshowJob>()
            .Property(j => j.Items)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<System.Collections.Generic.List<SlideItem>>(v, (JsonSerializerOptions?)null)
            );

        // TextToVideoJob 的 ProjectData 按 JSON 存储
        modelBuilder.Entity<TextToVideoJob>()
            .Property(j => j.ProjectData)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<VideoProject>(v, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            );
    }
}
