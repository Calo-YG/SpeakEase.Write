using SpeakEase.Write.Application.Contracts.Users;
using SpeakEase.Write.Application.Contracts.Users.Dto;

namespace SpeakEase.Write.MapRoute.Users
{
    public static class UserRoute
    {
        public static void MapUserEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/user")
               .WithDescription("用户管理")
               .WithTags("user")
               .RequireAuthorization();

            app.MapGet("api/user/profile", async (IUserApplication userApp, CancellationToken cancellationToken) =>
            {
                return await userApp.GetProfileAsync(cancellationToken);
            }).WithName("getprofile");

            app.MapPut("api/user/profile", async (UpdateProfileRequest request, IUserApplication userApp, CancellationToken cancellationToken) =>
            {
                return await userApp.UpdateProfileAsync(request, cancellationToken);
            }).WithName("updateprofile");

            app.MapPut("api/user/password", async (ChangePasswordRequest request, IUserApplication userApp, CancellationToken cancellationToken) =>
            {
                return await userApp.ChangePasswordAsync(request, cancellationToken);
            }).WithName("changepassword");
        }
    }
}
