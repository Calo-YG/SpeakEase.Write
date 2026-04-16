using AINWZ.Application.Contracts.Users;
using AINWZ.Application.Contracts.Users.Dto;

namespace AINWZ.MapRoute.Users
{
    public static class UserRoute
    {
        public static void MapUserEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/user")
               .WithDescription("用户管理")
               .WithTags("user")
               .RequireAuthorization();

            app.MapGet("api/user/profile", (IUserApplication userApp, CancellationToken cancellationToken) =>
            {
                return userApp.GetProfileAsync(cancellationToken);
            }).WithName("getprofile");

            app.MapPut("api/user/profile", (UpdateProfileRequest request, IUserApplication userApp, CancellationToken cancellationToken) =>
            {
                return userApp.UpdateProfileAsync(request, cancellationToken);
            }).WithName("updateprofile");

            app.MapPut("api/user/password", (ChangePasswordRequest request, IUserApplication userApp, CancellationToken cancellationToken) =>
            {
                return userApp.ChangePasswordAsync(request, cancellationToken);
            }).WithName("changepassword");
        }
    }
}
