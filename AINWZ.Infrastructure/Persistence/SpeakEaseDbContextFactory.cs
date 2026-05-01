using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpeakEase.Write.Infrastructure.Persistence;

/// <summary>
/// EF Core 设计时 DbContext 工厂，供 dotnet ef migrations 命令使用。
/// </summary>
internal sealed class SpeakEaseDbContextFactory : IDesignTimeDbContextFactory<SpeakEaseDbContext>
{
    public SpeakEaseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SpeakEaseDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=7452;Database=speakeasewrite;Username=blog;Password=blog123");

        return new SpeakEaseDbContext(optionsBuilder.Options);
    }
}
