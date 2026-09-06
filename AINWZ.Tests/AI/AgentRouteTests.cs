using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.MapRoute.AI;

namespace AINWZ.Tests.AI;

public sealed class AgentRouteTests
{
    [Fact]
    public async Task StreamEndpoint_MapsErrorChunkToErrorSseEvent()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddSingleton<IAgentApplication, ErrorAgentApplication>();
        builder.Services.AddSingleton<ISkilCapable, SkillCapable>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAgentEndPoint();
        await app.StartAsync();

        var response = await app.GetTestClient().PostAsync(
            "/ai/agent/chat/stream",
            new StringContent(JsonSerializer.Serialize(new AgentChatRequestDto
            {
                WorkId = "work-1",
                Messages = new List<AgentChatMessage>
                {
                    new() { Role = "user", Content = "hello" }
                }
            }), Encoding.UTF8, "application/json"));
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Contains("event: error\n", payload);
    }

    private sealed class ErrorAgentApplication : IAgentApplication
    {
        public Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentResponse { StopReason = "llm_error" });

        public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
            AgentChatRequestDto request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new AgentStreamChunk { Type = "error", Content = "failed" };
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", "user-1") },
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
