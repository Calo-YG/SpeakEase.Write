using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Users;
using SpeakEase.Write.Application.Contracts.Users.Dto;

namespace SpeakEase.Write.MapRoute.AI
{
    /// <summary>
    /// 模型提供商 + 用户模型配置 路由
    /// </summary>
    public static class ModelRoute
    {
        public static void MapModelEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/model")
               .WithDescription("模型管理")
               .WithTags("model")
               .RequireAuthorization();

            // === Provider 端点 ===

            group.MapGet("providers", async (IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.GetProvidersAsync(cancellationToken);
            }).WithName("getproviders");

            group.MapGet("providers/{id}", async (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.GetProviderByIdAsync(id, cancellationToken);
            }).WithName("getproviderbyid");

            group.MapPost("providers", async (SaveProviderRequest request, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.CreateProviderAsync(request, cancellationToken);
            }).WithName("createprovider");

            group.MapPut("providers/{id}", async (string id, SaveProviderRequest request, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.UpdateProviderAsync(id, request, cancellationToken);
            }).WithName("updateprovider");

            group.MapDelete("providers/{id}", async (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.DeleteProviderAsync(id, cancellationToken);
            }).WithName("deleteprovider");

            group.MapGet("providers/{id}/models", async (string id, IModelApplication modelApp, CancellationToken cancellationToken) =>
            {
                return await modelApp.GetProviderModelsAsync(id, cancellationToken);
            }).WithName("getprovidermodels");

            // === 用户模型配置端点 ===

            group.MapGet("configs", async (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.GetMyConfigsAsync(cancellationToken);
            }).WithName("getmymodelconfigs");

            group.MapGet("configs/active", async (IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.GetActiveConfigAsync(cancellationToken);
            }).WithName("getactivemodelconfig");

            group.MapPost("configs", async (SaveUserModelConfigRequest request, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.SaveConfigAsync(request, cancellationToken);
            }).WithName("savemodelconfig");

            group.MapPut("configs/{id}/activate", async (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.ActivateConfigAsync(id, cancellationToken);
            }).WithName("activatemodelconfig");

            group.MapDelete("configs/{id}", async (string id, IUserModelConfigApplication configApp, CancellationToken cancellationToken) =>
            {
                return await configApp.DeleteConfigAsync(id, cancellationToken);
            }).WithName("deletemodelconfig");
        }
    }
}
