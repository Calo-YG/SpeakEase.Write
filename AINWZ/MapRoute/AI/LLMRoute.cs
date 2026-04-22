namespace SpeakEase.Write.MapRoute.AI
{
    public static class LLMRoute
    {
        public static void MapLLMEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/llm")
               .WithDescription("LLM 服务")
               .WithTags("llm")
               .RequireAuthorization();
            // 在此处添加 LLM 相关的端点映射，例如：
            // group.MapPost("chat", ...).WithName("chat").RequireAuthorization();


        }
    }
}
