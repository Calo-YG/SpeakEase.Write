using AINWZ.Application.Contracts.AI;
using AINWZ.Application.Contracts.AI.Dto;
using AINWZ.Application.Contracts.Users;
using AINWZ.Application.Contracts.Users.Dto;

namespace AINWZ.MapRoute.AI
{
    public static class ModelRoute
    {
        public static void MapModelEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/model")
               .WithDescription("模型提供商管理")
               .WithTags("model")
               .RequireAuthorization();

            app.MapGet("api/model/providers", (IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return modelApp.GetProvidersAsync(cancellationToken);
            }).WithName("getproviders");

            app.MapGet("api/model/providers/{id}", (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return modelApp.GetProviderByIdAsync(id, cancellationToken);
            }).WithName("getproviderbyid");

            app.MapPost("api/model/providers", (SaveProviderRequest request, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return modelApp.CreateProviderAsync(request, cancellationToken);
            }).WithName("createprovider");

            app.MapPut("api/model/providers/{id}", (string id, SaveProviderRequest request, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return modelApp.UpdateProviderAsync(id, request, cancellationToken);
            }).WithName("updateprovider");

            app.MapDelete("api/model/providers/{id}", (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return modelApp.DeleteProviderAsync(id, cancellationToken);
            }).WithName("deleteprovider");

            // === 用户模型配置端点 ===

            app.MapGet("api/model/configs", (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.GetMyConfigsAsync(cancellationToken);
            }).WithName("getmymodelconfigs");

            app.MapGet("api/model/configs/active", (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.GetActiveConfigAsync(cancellationToken);
            }).WithName("getactivemodelconfig");

            app.MapPost("api/model/configs", (SaveUserModelConfigRequest request, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.SaveConfigAsync(request, cancellationToken);
            }).WithName("savemodelconfig");

            app.MapPut("api/model/configs/{id}/activate", (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.ActivateConfigAsync(id, cancellationToken);
            }).WithName("activatemodelconfig");

            app.MapDelete("api/model/configs/{id}", (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return configApp.DeleteConfigAsync(id, cancellationToken);
            }).WithName("deletemodelconfig");
        }
    }
}
