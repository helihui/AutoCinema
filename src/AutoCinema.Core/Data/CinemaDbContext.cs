using System.Text.Json;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace AutoCinema.Pro.Data;

public class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options) : base(options)
    {
    }

    public DbSet<ProjectState> Projects { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 Progress 属性的 JSON 序列化存储
        modelBuilder.Entity<ProjectState>()
            .Property(p => p.Progress)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<ProductionProgress>(v, (JsonSerializerOptions?)null)
            );

        // 这里如果未来有更多复杂对象，可以按需添加转换
    }
}
