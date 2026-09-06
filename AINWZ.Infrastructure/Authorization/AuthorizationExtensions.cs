using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Application.Abstractions.Authorization;
using ApplicationUserContext = SpeakEase.Write.Application.Abstractions.Identity.IUserContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SpeakEase.Authorization;
using SpeakEase.Authorization.Authorization;
using System.Text;

namespace SpeakEase.Write.Infrastructure.Authorization;

public static class AuthorizationExtensions
{
      public static IServiceCollection AddJwt(this IServiceCollection services,IConfiguration configuration)
      {
            var options = configuration.GetSection("JwtOptions").Get<JwtOptions>();
            services.Configure<JwtOptions>(configuration.GetSection("JwtOptions"));

            var secret = options!.SecretKey;
            var issuer = options.Issuer;
            var audience = options.Audience;
            var expire = options.ExpMinutes;


            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
               throw new Exception("validate jwt options failed");
            }

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(op =>
            {
                op.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true, // 检查过期时间
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    // 时钟偏移与令牌生命周期解耦，避免 ExpMinutes 配置错误时放大过期窗口。
                    ClockSkew = TimeSpan.FromMinutes(5),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                };
            });

            services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizeResultHandle>();
            services.AddScoped<UserContext>();
            services.AddScoped<IUserContext>(sp => sp.GetRequiredService<UserContext>());
            services.AddScoped<ApplicationUserContext>(sp => sp.GetRequiredService<UserContext>());
            services.AddScoped<IWorkAccessChecker, WorkAccessChecker>();
            services.AddScoped<SpeakEase.Authorization.Authorization.ITokenManager, TokenManager>();
            services.AddScoped<SpeakEase.Write.Application.Abstractions.Authorization.ITokenManager>(sp => sp.GetRequiredService<SpeakEase.Authorization.Authorization.ITokenManager>());
            return services;
      }
}
