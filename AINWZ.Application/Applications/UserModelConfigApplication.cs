using AINWZ.Application.Contracts.Users;
using AINWZ.Application.Contracts.Users.Dto;
using AINWZ.Domain.Entities.Users;
using AINWZ.Infrastructure.Ids;
using AINWZ.Infrastructure.Persistence;
using AINWZ.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AINWZ.Application.Applications
{
    /// <summary>
    /// 用户模型配置应用服务实现（主表：UserAiModelConfigEntity，关联：AIModelDefinitionEntity）。
    /// </summary>
    public class UserModelConfigApplication(
        AINWZDbContext dbContext,
        ISnowflakeIdGenerator idGenerator,
        IUserContext userContext,
        IHttpClientFactory httpClientFactory,
        ILogger<UserModelConfigApplication> logger) : IUserModelConfigApplication
    {
        /// <inheritdoc />
        public async Task<ApiResult<List<UserModelConfigResponse>>> GetMyConfigsAsync(CancellationToken cancellationToken = default)
        {
            var userId = userContext.UserId;

            var configs = await dbContext.UserAiModelConfigs
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.CreateAt)
                .Join(dbContext.AIModelDefinitions,
                    c => c.ProviderId,
                    p => p.Id,
                    (c, p) => new { c, ProviderLabel = p.Label })
                .GroupJoin(dbContext.AIModelDefinitions,
                    x => x.c.FallbackProviderId,
                    p => p.Id,
                    (x, fallbacks) => new { x.c, x.ProviderLabel, fallbacks })
                .SelectMany(
                    x => x.fallbacks.DefaultIfEmpty(),
                    (x, fallback) => new UserModelConfigResponse
                    {
                        Id = x.c.Id,
                        ConfigName = x.c.ConfigName,
                        ProviderId = x.c.ProviderId,
                        ProviderLabel = x.ProviderLabel,
                        ModelName = x.c.ModelName,
                        FallbackProviderId = x.c.FallbackProviderId,
                        FallbackProviderLabel = fallback != null ? fallback.Label : string.Empty,
                        FallbackModelName = x.c.FallbackModelName,
                        IsActive = x.c.IsActive,
                        UseFallback = x.c.UseFallback,
                        Preference = x.c.Preference,
                        Description = x.c.Description,
                        EstimateCost = x.c.EstimateCost,
                        ContextWindow = x.c.ContextWindow,
                        MaxOutputTokens = x.c.MaxOutputTokens,
                        SupportsStreaming = x.c.SupportsStreaming,
                        SupportsToolCall = x.c.SupportsToolCall,
                        CapabilityTags = x.c.CapabilityTags,
                        LastSyncedAt = x.c.LastSyncedAt,
                        CreateAt = x.c.CreateAt
                    })
                .ToListAsync(cancellationToken);

            return new ApiResult<List<UserModelConfigResponse>>(configs);
        }

        /// <inheritdoc />
        public async Task<ApiResult<UserModelConfigResponse>> GetActiveConfigAsync(CancellationToken cancellationToken = default)
        {
            var userId = userContext.UserId;

            var dto = await dbContext.UserAiModelConfigs
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .Join(dbContext.AIModelDefinitions,
                    c => c.ProviderId,
                    p => p.Id,
                    (c, p) => new { c, ProviderLabel = p.Label })
                .GroupJoin(dbContext.AIModelDefinitions,
                    x => x.c.FallbackProviderId,
                    p => p.Id,
                    (x, fallbacks) => new { x.c, x.ProviderLabel, fallbacks })
                .SelectMany(
                    x => x.fallbacks.DefaultIfEmpty(),
                    (x, fallback) => new UserModelConfigResponse
                    {
                        Id = x.c.Id,
                        ConfigName = x.c.ConfigName,
                        ProviderId = x.c.ProviderId,
                        ProviderLabel = x.ProviderLabel,
                        ModelName = x.c.ModelName,
                        FallbackProviderId = x.c.FallbackProviderId,
                        FallbackProviderLabel = fallback != null ? fallback.Label : string.Empty,
                        FallbackModelName = x.c.FallbackModelName,
                        IsActive = x.c.IsActive,
                        UseFallback = x.c.UseFallback,
                        Preference = x.c.Preference,
                        Description = x.c.Description,
                        EstimateCost = x.c.EstimateCost,
                        ContextWindow = x.c.ContextWindow,
                        MaxOutputTokens = x.c.MaxOutputTokens,
                        SupportsStreaming = x.c.SupportsStreaming,
                        SupportsToolCall = x.c.SupportsToolCall,
                        CapabilityTags = x.c.CapabilityTags,
                        LastSyncedAt = x.c.LastSyncedAt,
                        CreateAt = x.c.CreateAt
                    })
                .FirstOrDefaultAsync(cancellationToken);

            if (dto is null)
            {
                return new ApiResult<UserModelConfigResponse>("当前用户无激活配置。", 404);
            }

            return new ApiResult<UserModelConfigResponse>(dto);
        }

        /// <inheritdoc />
        public async Task<ApiResult<UserModelConfigResponse>> SaveConfigAsync(SaveUserModelConfigRequest request, CancellationToken cancellationToken = default)
        {
            // 参数验证
            if (string.IsNullOrWhiteSpace(request.ConfigName))
            {
                return new ApiResult<UserModelConfigResponse>("配置名称不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.ProviderId))
            {
                return new ApiResult<UserModelConfigResponse>("首选提供商不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                return new ApiResult<UserModelConfigResponse>("首选模型名称不能为空。", 400);
            }

            // 校验首选提供商存在且启用，并获取提供商信息
            var provider = await dbContext.AIModelDefinitions
                .AsNoTracking()
                .Where(x => x.Id == request.ProviderId && x.IsActive)
                .Select(x => new { x.Id, x.ApiBaseUrl, x.ApiKey, x.Label })
                .FirstOrDefaultAsync(cancellationToken);
            if (provider is null)
            {
                return new ApiResult<UserModelConfigResponse>("首选提供商不存在或未启用。", 400);
            }

            // 校验备用提供商（如有）
            string fallbackProvider = null;
            string fallbackApiBaseUrl = null;
            string fallbackApiKey = null;
            if (!string.IsNullOrWhiteSpace(request.FallbackProviderId))
            {
                var fb = await dbContext.AIModelDefinitions
                    .AsNoTracking()
                    .Where(x => x.Id == request.FallbackProviderId && x.IsActive)
                    .Select(x => new { x.Id, x.ApiBaseUrl, x.ApiKey, x.Label })
                    .FirstOrDefaultAsync(cancellationToken);
                if (fb is null)
                {
                    return new ApiResult<UserModelConfigResponse>("备用提供商不存在或未启用。", 400);
                }
                fallbackProvider = fb.Label;
                fallbackApiBaseUrl = fb.ApiBaseUrl;
                fallbackApiKey = fb.ApiKey;
            }

            // 检测首选模型 function call 能力
            var toolCallResult = await ValidateFunctionCallCapabilityAsync(
                provider.ApiBaseUrl, provider.ApiKey, request.ModelName, cancellationToken);
            if (!toolCallResult.SupportsToolCall)
            {
                return new ApiResult<UserModelConfigResponse>(
                    $"首选模型 {request.ModelName} 不支持 Function Call 能力：{toolCallResult.Message}", 400);
            }

            // 检测备用模型 function call 能力（如有）
            if (!string.IsNullOrWhiteSpace(request.FallbackModelName) && fallbackApiBaseUrl is not null)
            {
                var fallbackToolCallResult = await ValidateFunctionCallCapabilityAsync(
                    fallbackApiBaseUrl, fallbackApiKey!, request.FallbackModelName, cancellationToken);
                if (!fallbackToolCallResult.SupportsToolCall)
                {
                    return new ApiResult<UserModelConfigResponse>(
                        $"备用模型 {request.FallbackModelName} 不支持 Function Call 能力：{fallbackToolCallResult.Message}", 400);
                }
            }

            var userId = userContext.UserId;

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                // === 创建 ===
                // ConfigName 唯一性校验
                var nameDuplicate = await dbContext.UserAiModelConfigs
                    .AnyAsync(x => x.UserId == userId && x.ConfigName == request.ConfigName, cancellationToken);
                if (nameDuplicate)
                {
                    return new ApiResult<UserModelConfigResponse>($"配置名称 \"{request.ConfigName}\" 已存在。", 400);
                }

                // 判断是否为用户的第一个配置，自动激活
                var hasAnyConfig = await dbContext.UserAiModelConfigs
                    .AnyAsync(x => x.UserId == userId, cancellationToken);

                var entity = new UserAiModelConfigEntity
                {
                    Id = idGenerator.NextIdString(),
                    UserId = userId,
                    ConfigName = request.ConfigName,
                    ProviderId = request.ProviderId,
                    ModelName = request.ModelName,
                    FallbackProviderId = request.FallbackProviderId,
                    FallbackModelName = request.FallbackModelName,
                    IsActive = !hasAnyConfig, // 第一个配置自动激活
                    UseFallback = request.UseFallback,
                    Preference = request.Preference,
                    Description = request.Description,
                    EstimateCost = request.EstimateCost,
                    ContextWindow = request.ContextWindow,
                    MaxOutputTokens = request.MaxOutputTokens,
                    SupportsStreaming = request.SupportsStreaming,
                    SupportsToolCall = true, // 已通过验证
                    CapabilityTags = request.CapabilityTags,
                    LastSyncedAt = DateTime.UtcNow
                };

                dbContext.UserAiModelConfigs.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("用户 {UserId} 创建模型配置：{ConfigName}，Id={Id}", userId, entity.ConfigName, entity.Id);

                return await BuildResponseAsync(entity, cancellationToken);
            }
            else
            {
                // === 更新 ===
                var entity = await dbContext.UserAiModelConfigs
                    .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

                if (entity is null)
                {
                    return new ApiResult<UserModelConfigResponse>($"未找到标识为 {request.Id} 的配置。", 404);
                }

                // ConfigName 唯一性校验（排除自身）
                var nameDuplicate = await dbContext.UserAiModelConfigs
                    .AnyAsync(x => x.UserId == userId && x.ConfigName == request.ConfigName && x.Id != request.Id, cancellationToken);
                if (nameDuplicate)
                {
                    return new ApiResult<UserModelConfigResponse>($"配置名称 \"{request.ConfigName}\" 已存在。", 400);
                }

                entity.ConfigName = request.ConfigName;
                entity.ProviderId = request.ProviderId;
                entity.ModelName = request.ModelName;
                entity.FallbackProviderId = request.FallbackProviderId;
                entity.FallbackModelName = request.FallbackModelName;
                entity.UseFallback = request.UseFallback;
                entity.Preference = request.Preference;
                entity.Description = request.Description;
                entity.EstimateCost = request.EstimateCost;
                entity.ContextWindow = request.ContextWindow;
                entity.MaxOutputTokens = request.MaxOutputTokens;
                entity.SupportsStreaming = request.SupportsStreaming;
                entity.SupportsToolCall = true; // 已通过验证
                entity.CapabilityTags = request.CapabilityTags;
                entity.LastSyncedAt = DateTime.UtcNow;
                entity.UpdateAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("用户 {UserId} 更新模型配置：{ConfigName}，Id={Id}", userId, entity.ConfigName, entity.Id);

                return await BuildResponseAsync(entity, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task<ApiResult<UserModelConfigResponse>> ActivateConfigAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new ApiResult<UserModelConfigResponse>("配置标识不能为空。", 400);
            }

            var userId = userContext.UserId;

            var entity = await dbContext.UserAiModelConfigs
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

            if (entity is null)
            {
                return new ApiResult<UserModelConfigResponse>($"未找到标识为 {id} 的配置。", 404);
            }

            if (entity.IsActive)
            {
                // 已经是激活状态，直接返回
                return await BuildResponseAsync(entity, cancellationToken);
            }

            // 取消同用户其他配置的激活状态
            var otherConfigs = await dbContext.UserAiModelConfigs
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var c in otherConfigs)
            {
                c.IsActive = false;
                c.UpdateAt = DateTime.UtcNow;
            }

            entity.IsActive = true;
            entity.UpdateAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("用户 {UserId} 激活模型配置：{ConfigName}，Id={Id}", userId, entity.ConfigName, entity.Id);

            return await BuildResponseAsync(entity, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ApiResult> DeleteConfigAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new ApiResult("配置标识不能为空。", 400);
            }

            var userId = userContext.UserId;

            var entity = await dbContext.UserAiModelConfigs
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

            if (entity is null)
            {
                return new ApiResult($"未找到标识为 {id} 的配置。", 404);
            }

            var wasActive = entity.IsActive;

            dbContext.UserAiModelConfigs.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("用户 {UserId} 删除模型配置：{ConfigName}，Id={Id}", userId, entity.ConfigName, entity.Id);

            // 若删除的是激活配置，自动激活最近的一条
            if (wasActive)
            {
                var latestConfig = await dbContext.UserAiModelConfigs
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.UpdateAt)
                    .ThenByDescending(x => x.CreateAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestConfig is not null)
                {
                    latestConfig.IsActive = true;
                    latestConfig.UpdateAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation("用户 {UserId} 自动激活配置：{ConfigName}，Id={Id}", userId, latestConfig.ConfigName, latestConfig.Id);
                }
            }

            return new ApiResult(true);
        }

        /// <summary>
        /// 根据实体构建响应 DTO（含关联提供商展示名称）。
        /// </summary>
        private async Task<ApiResult<UserModelConfigResponse>> BuildResponseAsync(UserAiModelConfigEntity entity, CancellationToken cancellationToken)
        {
            var providerIds = new List<string> { entity.ProviderId };
            if (!string.IsNullOrWhiteSpace(entity.FallbackProviderId))
            {
                providerIds.Add(entity.FallbackProviderId);
            }

            var providerLabels = await dbContext.AIModelDefinitions
                .AsNoTracking()
                .Where(x => providerIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Label })
                .ToDictionaryAsync(x => x.Id, x => x.Label, cancellationToken);

            providerLabels.TryGetValue(entity.ProviderId, out var providerLabel);
            providerLabels.TryGetValue(entity.FallbackProviderId ?? string.Empty, out var fallbackLabel);

            return new ApiResult<UserModelConfigResponse>(new UserModelConfigResponse
            {
                Id = entity.Id,
                ConfigName = entity.ConfigName,
                ProviderId = entity.ProviderId,
                ProviderLabel = providerLabel ?? string.Empty,
                ModelName = entity.ModelName,
                FallbackProviderId = entity.FallbackProviderId,
                FallbackProviderLabel = fallbackLabel ?? string.Empty,
                FallbackModelName = entity.FallbackModelName,
                IsActive = entity.IsActive,
                UseFallback = entity.UseFallback,
                Preference = entity.Preference,
                Description = entity.Description,
                EstimateCost = entity.EstimateCost,
                ContextWindow = entity.ContextWindow,
                MaxOutputTokens = entity.MaxOutputTokens,
                SupportsStreaming = entity.SupportsStreaming,
                SupportsToolCall = entity.SupportsToolCall,
                CapabilityTags = entity.CapabilityTags,
                LastSyncedAt = entity.LastSyncedAt,
                CreateAt = entity.CreateAt
            });
        }

        /// <summary>
        /// 通过 OpenAI 兼容 API 检测模型是否支持 Function Call 能力。
        /// 发送一个带有 tools 定义的最小 chat completions 请求，
        /// 若 API 接受请求（HTTP 200）则视为支持，若返回工具相关错误则视为不支持。
        /// </summary>
        private async Task<(bool SupportsToolCall, string Message)> ValidateFunctionCallCapabilityAsync(
            string baseUrl, string apiKey, string modelName, CancellationToken cancellationToken)
        {
            try
            {
                var url = baseUrl.TrimEnd('/') + "/chat/completions";

                logger.LogInformation("开始检测模型 Function Call 能力：{Url}，模型={Model}", url, modelName);

                using var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // 构造最小化 chat completions 请求，含一个工具定义
                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "user", content = "reply ok" }
                    },
                    max_tokens = 1,
                    tools = new[]
                    {
                        new
                        {
                            type = "function",
                            @function = new
                            {
                                name = "_capability_probe",
                                description = "Internal capability probe",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new { }
                                }
                            }
                        }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(requestBody)
                };
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("模型 {Model} 支持 Function Call 能力", modelName);
                    return (true, string.Empty);
                }

                // 读取错误响应体
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("模型 Function Call 检测失败：{Url}，模型={Model}，状态码={StatusCode}，响应={Body}",
                    url, modelName, (int)response.StatusCode, errorBody);

                // 401 → 认证问题
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "API 密钥无效或未授权。");
                }

                // 404 → 模型不存在
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (false, $"模型 {modelName} 不存在。");
                }

                // 尝试从错误响应中提取具体信息
                var errorMessage = ExtractErrorMessage(errorBody);

                // 判断是否为工具/功能调用不支持的错误
                if (IsToolCallNotSupportedError(errorBody))
                {
                    return (false, $"模型 {modelName} 不支持 Function Call。");
                }

                return (false, $"API 返回错误（HTTP {(int)response.StatusCode}）：{errorMessage}");
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // 取消令牌触发，不吞掉
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "模型 Function Call 检测超时：{Model}", modelName);
                return (false, $"模型 {modelName} 检测超时，请检查网络或提供商地址。");
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "模型 Function Call 检测网络错误：{Model}", modelName);
                return (false, $"连接失败：{ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "模型 Function Call 检测未知异常：{Model}", modelName);
                return (false, $"检测异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 从 API 错误响应体中提取错误消息。
        /// </summary>
        private static string ExtractErrorMessage(string errorBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(errorBody);
                var root = doc.RootElement;

                // OpenAI 标准格式: { "error": { "message": "..." } }
                if (root.TryGetProperty("error", out var errorObj) &&
                    errorObj.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString() ?? errorBody;
                }

                return errorBody;
            }
            catch
            {
                return errorBody;
            }
        }

        /// <summary>
        /// 判断 API 错误响应是否表明模型不支持工具/功能调用。
        /// 匹配常见提供商的错误模式：tool、function calling 不支持等。
        /// </summary>
        private static bool IsToolCallNotSupportedError(string errorBody)
        {
            if (string.IsNullOrWhiteSpace(errorBody)) return false;

            var lower = errorBody.ToLowerInvariant();

            // 常见不支持工具调用的错误关键词
            var patterns = new[]
            {
                "does not support tools",
                "does not support function",
                "does not support tool_call",
                "tool use is not supported",
                "function calling is not supported",
                "tools are not supported",
                "tool calling is not available",
                "not support tool",
                "not support function call",
                "tool is not available"
            };

            return patterns.Any(p => lower.Contains(p));
        }
    }
}
