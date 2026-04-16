using AINWZ.Application.Contracts.AI;
using AINWZ.Application.Contracts.AI.Dto;

namespace AINWZ.MapRoute.AI
{
    public static class LLMLogRoute
    {
        public static void MapLLMLogEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/llmlog")
               .WithDescription("LLM 日志管理")
               .WithTags("llm")
               .RequireAuthorization();

            app.MapPost("api/llmlog/query", async (LLMCallLogQueryRequest request, ILLMCallLogApplication logApp, CancellationToken cancellationToken) =>
            {
                return logApp.GetPagedAsync(request, cancellationToken);
            }).WithName("query");

            app.MapGet("api/llmlog/{id}", async (string id,ILLMCallLogApplication logApp, CancellationToken cancellationToken) =>
            {
                return logApp.GetByIdAsync(id, cancellationToken);
            }).WithName("getbyid");
        }
    }
}
