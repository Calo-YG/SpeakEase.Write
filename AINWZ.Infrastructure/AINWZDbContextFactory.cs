using AINWZ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AINWZ.Infrastructure;

/// <summary>
/// EF Core 设计时 DbContext 工厂，供 dotnet ef migrations 命令使用。
/// </summary>
internal sealed class AINWZDbContextFactory : IDesignTimeDbContextFactory<AINWZDbContext>
{
    public AINWZDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AINWZDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=7452;Database=ainwz;Username=blog;Password=blog123");

        return new AINWZDbContext(optionsBuilder.Options);
    }
}
