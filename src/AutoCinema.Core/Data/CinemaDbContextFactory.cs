using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace AutoCinema.Pro.Data;

public class CinemaDbContextFactory : IDesignTimeDbContextFactory<CinemaDbContext>
{
    public CinemaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CinemaDbContext>();
        optionsBuilder.UseSqlite(@"Data Source=e:\100.Work\NestCoreProject\AutoCinema\src\db\autocinema.db");

        return new CinemaDbContext(optionsBuilder.Options);
    }
}
