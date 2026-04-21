using AINWZ.Application.Contracts.AI.Dto;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AINWZ.MapRoute.AI
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
