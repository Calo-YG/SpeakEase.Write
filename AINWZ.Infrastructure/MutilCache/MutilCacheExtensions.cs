using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AINWZ.Infrastructure.MutilCache
{
    /// <summary>
    /// 多级缓存扩展方法
    /// </summary>
    public static class MutilCacheExtensions
    {
        public static IServiceCollection AddMutilCache(this IServiceCollection services,IConfiguration configuration)
        {
            services.AddMemoryCache();
            services.AddSingleton<IMultiCacheService, MultiCacheService>();

            var redisConfig = configuration.GetConnectionString("Redis") ?? configuration["Redis"];

            if (!string.IsNullOrWhiteSpace(redisConfig))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConfig;
                    options.InstanceName = "AINWZ:";
                });
            }
            else
            {
                // 无 Redis 配置时使用内存分布式缓存作为降级
                services.AddDistributedMemoryCache();
            }

            services.AddDistributedMemoryCache();

            return services;
        }
    }
}
