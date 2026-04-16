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
            services.AddSingleton<IMultiCacheService, MultiCacheService>();

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "AINWZ:";
            });

            services.AddDistributedMemoryCache();

            return services;
        }
    }
}
