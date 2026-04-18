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

            app.MapGet("api/model/providers", async (IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.GetProvidersAsync(cancellationToken);
            }).WithName("getproviders");

            app.MapGet("api/model/providers/{id}", async (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.GetProviderByIdAsync(id, cancellationToken);
            }).WithName("getproviderbyid");

            app.MapPost("api/model/providers", async (SaveProviderRequest request, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.CreateProviderAsync(request, cancellationToken);
            }).WithName("createprovider");

            app.MapPut("api/model/providers/{id}", async (string id, SaveProviderRequest request, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.UpdateProviderAsync(id, request, cancellationToken);
            }).WithName("updateprovider");

            app.MapDelete("api/model/providers/{id}", async (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.DeleteProviderAsync(id, cancellationToken);
            }).WithName("deleteprovider");

            // === 用户模型配置端点 ===

            app.MapGet("api/model/configs", async (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.GetMyConfigsAsync(cancellationToken);
            }).WithName("getmymodelconfigs");

            app.MapGet("api/model/configs/active", async (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.GetActiveConfigAsync(cancellationToken);
            }).WithName("getactivemodelconfig");

            app.MapPost("api/model/configs", async (SaveUserModelConfigRequest request, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.SaveConfigAsync(request, cancellationToken);
            }).WithName("savemodelconfig");

            app.MapPut("api/model/configs/{id}/activate", async (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.ActivateConfigAsync(id, cancellationToken);
            }).WithName("activatemodelconfig");

            app.MapDelete("api/model/configs/{id}", async (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.DeleteConfigAsync(id, cancellationToken);
            }).WithName("deletemodelconfig");
        }
    }
}
