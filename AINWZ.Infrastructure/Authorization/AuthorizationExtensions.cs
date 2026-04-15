using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SpeakEase.Authorization.Authorization;
using System.Text;

namespace AINWZ.Infrastructure.Authorization;

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
                    ClockSkew = TimeSpan.FromSeconds(expire),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                };
            });

            services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizeResultHandle>();
            services.AddScoped<IUserContext, UserContext>();
            services.AddScoped<ITokenManager,TokenManager>();
            return services;
      }
}