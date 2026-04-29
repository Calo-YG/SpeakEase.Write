using Microsoft.Extensions.Diagnostics.HealthChecks;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.HealthChecks;

public sealed class DbContextHealthCheck : IHealthCheck
{
    private readonly SpeakEaseDbContext _db;
    private readonly ILogger<DbContextHealthCheck> _logger;

    public DbContextHealthCheck(SpeakEaseDbContext db, ILogger<DbContextHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
                return HealthCheckResult.Healthy("数据库连接正常");

            return HealthCheckResult.Unhealthy("数据库连接失败");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康检查 - 数据库连接异常");
            return HealthCheckResult.Unhealthy("数据库连接异常", ex);
        }
    }
}
