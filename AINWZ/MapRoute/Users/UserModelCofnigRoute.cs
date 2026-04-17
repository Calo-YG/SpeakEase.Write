using AINWZ.Application.Contracts.Users;
using AINWZ.Application.Contracts.Users.Dto;

namespace AINWZ.MapRoute.Users
{
    public static class UserModelCofnigRoute
    {

        public static void MapUserModelConfigEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/usermodelconfig")
               .WithDescription("用户模型配置管理")
               .WithTags("usermodelconfig")
               .RequireAuthorization();

            // === 用户模型配置端点 ===

            app.MapGet("api/usermodelconfig/configs", (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.GetMyConfigsAsync(cancellationToken);
            }).WithName("getmymodelconfigs");

            app.MapGet("api/usermodelconfig/configs/active", (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.GetActiveConfigAsync(cancellationToken);
            }).WithName("getactivemodelconfig");

            app.MapPost("api/usermodelconfig/configs", (SaveUserModelConfigRequest request, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.SaveConfigAsync(request, cancellationToken);
            }).WithName("savemodelconfig");

            app.MapPut("api/usermodelconfig/configs/{id}/activate", (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.ActivateConfigAsync(id, cancellationToken);
            }).WithName("activatemodelconfig");

            app.MapDelete("api/usermodelconfig/configs/{id}", (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.DeleteConfigAsync(id, cancellationToken);
            }).WithName("deletemodelconfig");
        }
    }
}
