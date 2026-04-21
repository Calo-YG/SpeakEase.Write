using AINWZ.Domain.Entities.AI;
using AINWZ.Domain.Entities.Users;
using AINWZ.Infrastructure.LLM.Options;
using AINWZ.Infrastructure.LLM.Providers;
using AINWZ.Infrastructure.MutilCache;
using AINWZ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using SpeakEase.Authorization.Authorization;

namespace AINWZ.Tests.LLM;

public class CurrentLLMOptionsResolverTests
{
    private static (CurrentLLMOptionsResolver resolver, SpeakEaseDbContext db) CreateSut(
        string userId = "user123",
        Action<SpeakEaseDbContext> seed = null)
    {
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        var db = new SpeakEaseDbContext(options);
        seed?.Invoke(db);
        db.SaveChanges();

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.UserId).Returns(userId);

        var fallbackOptions = Options.Create(new LLMOptions
        {
            BaseUrl = "https://fallback.api.com/v1",
            ApiKey = "fallback-key",
            DefaultModel = "fallback-model",
            TimeoutSeconds = 60
        });

        var cache = new Mock<IMultiCacheService>();
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<CurrentLLMOptions>>>(),
                It.IsAny<Action>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<int>())
            ).Returns<string, Func<Task<CurrentLLMOptions>>, Action, TimeSpan?, TimeSpan?, int>(
                (_, factory, _, _, _, _) => factory());

        var resolver = new CurrentLLMOptionsResolver(db, userContext.Object, fallbackOptions, cache.Object);
        return (resolver, db);
    }

    [Fact]
    public async Task GetCurrentOptionsAsync_UserHasActiveConfig_ReturnsUserConfig()
    {
        // Arrange
        var (resolver, db) = CreateSut(seed: db =>
        {
            db.AIModelDefinitions.Add(new AIModelDefinitionEntity
            {
                Id = "provider1",
                Provider = "test",
                Label = "Test Provider",
                ApiBaseUrl = "https://user.api.com/v1",
                ApiKey = "user-key",
                IsActive = true
            });
            db.UserAiModelConfigs.Add(new UserAiModelConfigEntity
            {
                Id = "config1",
                UserId = "user123",
                ConfigName = "MyConfig",
                ProviderId = "provider1",
                ModelName = "user-model",
                IsActive = true,
                SupportsToolCall = true
            });
        });

        // Act
        var result = await resolver.GetCurrentOptionsAsync();

        // Assert
        Assert.Equal("https://user.api.com/v1", result.BaseUrl);
        Assert.Equal("user-key", result.ApiKey);
        Assert.Equal("user-model", result.DefaultModel);
    }

    [Fact]
    public async Task GetCurrentOptionsAsync_UserHasNoActiveConfig_ReturnsFallback()
    {
        // Arrange - 不 seed 任何数据
        var (resolver, _) = CreateSut();

        // Act
        var result = await resolver.GetCurrentOptionsAsync();

        // Assert
        Assert.Equal("https://fallback.api.com/v1", result.BaseUrl);
        Assert.Equal("fallback-key", result.ApiKey);
        Assert.Equal("fallback-model", result.DefaultModel);
    }

    [Fact]
    public async Task GetCurrentOptionsAsync_UserHasInactiveConfig_ReturnsFallback()
    {
        // Arrange
        var (resolver, db) = CreateSut(seed: db =>
        {
            db.AIModelDefinitions.Add(new AIModelDefinitionEntity
            {
                Id = "provider1",
                Provider = "test",
                Label = "Test",
                ApiBaseUrl = "https://user.api.com/v1",
                ApiKey = "user-key",
                IsActive = true
            });
            db.UserAiModelConfigs.Add(new UserAiModelConfigEntity
            {
                Id = "config1",
                UserId = "user123",
                ConfigName = "InactiveConfig",
                ProviderId = "provider1",
                ModelName = "user-model",
                IsActive = false,  // 非激活
                SupportsToolCall = true
            });
        });

        // Act
        var result = await resolver.GetCurrentOptionsAsync();

        // Assert
        Assert.Equal("https://fallback.api.com/v1", result.BaseUrl);
    }

    [Fact]
    public async Task GetCurrentOptionsAsync_WithFallbackModelSameProvider_PopulatesFallbackModels()
    {
        // Arrange
        var (resolver, db) = CreateSut(seed: db =>
        {
            db.AIModelDefinitions.Add(new AIModelDefinitionEntity
            {
                Id = "provider1",
                Provider = "test",
                Label = "Test",
                ApiBaseUrl = "https://user.api.com/v1",
                ApiKey = "user-key",
                IsActive = true
            });
            db.UserAiModelConfigs.Add(new UserAiModelConfigEntity
            {
                Id = "config1",
                UserId = "user123",
                ConfigName = "WithFallback",
                ProviderId = "provider1",
                ModelName = "primary-model",
                FallbackProviderId = "provider1",  // 同提供商
                FallbackModelName = "fallback-model-name",
                IsActive = true,
                UseFallback = true,
                SupportsToolCall = true
            });
        });

        // Act
        var result = await resolver.GetCurrentOptionsAsync();

        // Assert
        Assert.Single(result.FallbackModels);
        Assert.Equal("fallback-model-name", result.FallbackModels[0]);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedOptions()
    {
        // Arrange
        var cache = new Mock<IMultiCacheService>();
        var db = new SpeakEaseDbContext(new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}").Options);

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.UserId).Returns("user123");

        var fallbackOptions = Options.Create(new LLMOptions { BaseUrl = "", ApiKey = "", DefaultModel = "fb" });
        var resolver = new CurrentLLMOptionsResolver(db, userContext.Object, fallbackOptions, cache.Object);

        // Act
        await resolver.InvalidateAsync();

        // Assert
        cache.Verify(c => c.RemoveAsync(
            It.Is<string>(key => key.Contains("user123"))), Times.Once);
    }
}
