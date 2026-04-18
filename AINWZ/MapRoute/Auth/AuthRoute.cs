using AINWZ.Application.Contracts.Auth;
using AINWZ.Application.Contracts.Auth.Dto;

namespace AINWZ.MapRoute.Auth
{
    public static class AuthRoute
    {
        public static void MapAuthEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/auth")
               .WithDescription("认证管理")
               .WithTags("auth");

            app.MapPost("api/auth/register", async (RegisterRequest request, IAuthApplication authApp, CancellationToken cancellationToken) =>
            {
                return await authApp.RegisterAsync(request, cancellationToken);
            }).WithName("register");

            app.MapPost("api/auth/login", async (LoginRequest request, IAuthApplication authApp, CancellationToken cancellationToken) =>
            {
                return await authApp.LoginAsync(request, cancellationToken);
            }).WithName("login");

            app.MapPost("api/auth/refresh-token", async (RefreshTokenRequest request, IAuthApplication authApp, CancellationToken cancellationToken) =>
            {
                return await authApp.RefreshTokenAsync(request, cancellationToken);
            }).WithName("refreshtoken");
        }
    }
}
