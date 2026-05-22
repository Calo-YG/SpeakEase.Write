using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Infrastructure.Shared;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.Write.Application.Applications
{
    /// <summary>
    /// 模型提供商管理应用服务实现（单表：AIModelDefinitionEntity）。
    /// </summary>
    public class ModelApplication(
        SpeakEaseDbContext dbContext,
        ISnowflakeIdGenerator idGenerator,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelApplication> logger) : IModelApplication
    {
        /// <inheritdoc />
        public async Task<ApiResult<List<ModelProviderResponse>>> GetProvidersAsync(CancellationToken cancellationToken = default)
        {
            var list = await dbContext.AIModelDefinitions
                .AsNoTracking()
                .OrderByDescending(x => x.CreateAt)
                .Select(x => new ModelProviderResponse
                {
                    Id = x.Id,
                    Provider = x.Provider,
                    Label = x.Label,
                    Description = x.Description,
                    ApiBaseUrl = x.ApiBaseUrl,
                    IsActive = x.IsActive,
                    CreateAt = x.CreateAt
                })
                .ToListAsync(cancellationToken);

            return new ApiResult<List<ModelProviderResponse>>(list);
        }

        /// <inheritdoc />
        public async Task<ApiResult<ModelProviderResponse>> GetProviderByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new ApiResult<ModelProviderResponse>("标识不能为空。", 400);
            }

            var dto = await dbContext.AIModelDefinitions
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new ModelProviderResponse
                {
                    Id = x.Id,
                    Provider = x.Provider,
                    Label = x.Label,
                    Description = x.Description,
                    ApiBaseUrl = x.ApiBaseUrl,
                    IsActive = x.IsActive,
                    CreateAt = x.CreateAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (dto is null)
            {
                return new ApiResult<ModelProviderResponse>($"未找到标识为 {id} 的提供商。", 404);
            }

            return new ApiResult<ModelProviderResponse>(dto);
        }

        /// <inheritdoc />
        public async Task<ApiResult<ModelProviderResponse>> CreateProviderAsync(SaveProviderRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Provider))
            {
                return new ApiResult<ModelProviderResponse>("Provider 不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.Label))
            {
                return new ApiResult<ModelProviderResponse>("Label 不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.ApiBaseUrl))
            {
                return new ApiResult<ModelProviderResponse>("ApiBaseUrl 不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new ApiResult<ModelProviderResponse>("ApiKey 不能为空。", 400);
            }

            //var exists = await dbContext.AIModelDefinitions
            //    .AnyAsync(x => x.Provider == request.Provider, cancellationToken);
            //if (exists)
            //{
            //    return new ApiResult<ModelProviderResponse>($"提供商标识 {request.Provider} 已存在。", 400);
            //}

            // 验证 API 可用性
            var validationResult = await ValidateApiConnectivityAsync(request.ApiBaseUrl, request.ApiKey, cancellationToken);
            if (!validationResult.Successed)
            {
                return new ApiResult<ModelProviderResponse>(validationResult.Message, validationResult.Status);
            }

            var entity = new AIModelDefinitionEntity
            {
                Id = idGenerator.NextIdString(),
                Provider = request.Provider,
                Label = request.Label,
                Description = request.Description,
                ApiBaseUrl = request.ApiBaseUrl,
                ApiKey = request.ApiKey,
                IsActive = request.IsActive
            };

            dbContext.AIModelDefinitions.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("创建模型提供商：{Provider}（{Label}），Id={Id}", entity.Provider, entity.Label, entity.Id);

            return new ApiResult<ModelProviderResponse>(new ModelProviderResponse
            {
                Id = entity.Id,
                Provider = entity.Provider,
                Label = entity.Label,
                Description = entity.Description,
                ApiBaseUrl = entity.ApiBaseUrl,
                IsActive = entity.IsActive,
                CreateAt = entity.CreateAt
            });
        }

        /// <inheritdoc />
        public async Task<ApiResult<ModelProviderResponse>> UpdateProviderAsync(string id, SaveProviderRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new ApiResult<ModelProviderResponse>("标识不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.Provider))
            {
                return new ApiResult<ModelProviderResponse>("Provider 不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.Label))
            {
                return new ApiResult<ModelProviderResponse>("Label 不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.ApiBaseUrl))
            {
                return new ApiResult<ModelProviderResponse>("ApiBaseUrl 不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new ApiResult<ModelProviderResponse>("ApiKey 不能为空。", 400);
            }

            var entity = await dbContext.AIModelDefinitions
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                return new ApiResult<ModelProviderResponse>($"未找到标识为 {id} 的提供商。", 404);
            }

            // Provider 唯一性校验（排除自身）
            var duplicate = await dbContext.AIModelDefinitions
                .AnyAsync(x => x.Provider == request.Provider && x.Id != id, cancellationToken);
            if (duplicate)
            {
                return new ApiResult<ModelProviderResponse>($"提供商标识 {request.Provider} 已存在。", 400);
            }

            // 验证 API 可用性（仅在 ApiBaseUrl 或 ApiKey 发生变更时校验）
            var urlChanged = entity.ApiBaseUrl != request.ApiBaseUrl;
            var keyChanged = entity.ApiKey != request.ApiKey;
            if (urlChanged || keyChanged)
            {
                var validationResult = await ValidateApiConnectivityAsync(request.ApiBaseUrl, request.ApiKey, cancellationToken);
                if (!validationResult.Successed)
                {
                    return new ApiResult<ModelProviderResponse>(validationResult.Message, validationResult.Status);
                }
            }

            entity.Provider = request.Provider;
            entity.Label = request.Label;
            entity.Description = request.Description;
            entity.ApiBaseUrl = request.ApiBaseUrl;
            entity.ApiKey = request.ApiKey;
            entity.IsActive = request.IsActive;
            entity.UpdateAt = DateTime.Now;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("更新模型提供商：{Provider}（{Label}），Id={Id}", entity.Provider, entity.Label, entity.Id);

            return new ApiResult<ModelProviderResponse>(new ModelProviderResponse
            {
                Id = entity.Id,
                Provider = entity.Provider,
                Label = entity.Label,
                Description = entity.Description,
                ApiBaseUrl = entity.ApiBaseUrl,
                IsActive = entity.IsActive,
                CreateAt = entity.CreateAt
            });
        }

        /// <inheritdoc />
        public async Task<ApiResult> DeleteProviderAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new ApiResult("标识不能为空。", 400);
            }

            var entity = await dbContext.AIModelDefinitions
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                return new ApiResult($"未找到标识为 {id} 的提供商。", 404);
            }

            dbContext.AIModelDefinitions.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("删除模型提供商：{Provider}（{Label}），Id={Id}", entity.Provider, entity.Label, entity.Id);

            return new ApiResult(true);
        }

        /// <inheritdoc />
        public async Task<ApiResult<List<string>>> GetProviderModelsAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                return new ApiResult<List<string>>("标识不能为空。", 400);
        
            var entity = await dbContext.AIModelDefinitions
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.ApiBaseUrl, x.ApiKey })
                .FirstOrDefaultAsync(cancellationToken);
        
            if (entity is null)
                return new ApiResult<List<string>>($"未找到标识为 {id} 的提供商。", 404);
        
            try
            {
                var url = entity.ApiBaseUrl.TrimEnd('/') + "/models";
                logger.LogInformation("获取提供商模型列表：{Url}", url);
        
                using var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(15);
        
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {entity.ApiKey}");
        
                using var response = await httpClient.SendAsync(request, cancellationToken);
        
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogWarning("获取模型列表失败：{Url}，状态码={StatusCode}，响应={Body}", url, (int)response.StatusCode, body);
                    return new ApiResult<List<string>>($"获取模型列表失败（HTTP {(int)response.StatusCode}）。", 400);
                }
        
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var parsed = await JsonSerializer.DeserializeAsync<OpenAiModelsResponse>(stream,
                    JsonHelper.DefaultOptions, cancellationToken);
        
                var models = parsed?.Data
                    ?.Select(m => m.Id)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .OrderBy(s => s)
                    .ToList() ?? [];
        
                logger.LogInformation("提供商 {Id} 共返回 {Count} 个模型", id, models.Count);
                return new ApiResult<List<string>>(models);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "获取模型列表超时：{BaseUrl}", entity.ApiBaseUrl);
                return new ApiResult<List<string>>("获取模型列表超时，请检查提供商地址。", 400);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "获取模型列表网络错误：{BaseUrl}", entity.ApiBaseUrl);
                return new ApiResult<List<string>>($"获取模型列表失败：{ex.Message}", 400);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取模型列表未知异常：{BaseUrl}", entity.ApiBaseUrl);
                return new ApiResult<List<string>>($"获取模型列表异常：{ex.Message}", 400);
            }
        }
        
        /// <summary>
        /// 按 OpenAI 兼容格式验证 API 地址与密鑰可用性。
        /// 请求 GET {baseUrl}/models，若返回 2xx 则视为可用。
        /// </summary>
        private async Task<ApiResult> ValidateApiConnectivityAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
        {
            try
            {
                // 规范化 baseUrl，确保末尾有 /
                var url = baseUrl.TrimEnd('/') + "/models";

                logger.LogInformation("开始验证 API 可用性：{Url}", url);

                using var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(15);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("API 可用性验证通过：{Url}", url);
                    return new ApiResult(true);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("API 可用性验证失败：{Url}，状态码={StatusCode}，响应={Body}", url, (int)response.StatusCode, body);

                return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? new ApiResult("API 密钥无效或未授权。", 400)
                    : new ApiResult($"API 地址不可用（HTTP {(int)response.StatusCode}），请检查地址和密钥。", 400);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // 取消令牌触发，不吞掉
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "API 可用性验证超时：{BaseUrl}", baseUrl);
                return new ApiResult("API 连接超时，请检查地址是否正确。", 400);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "API 可用性验证网络错误：{BaseUrl}", baseUrl);
                return new ApiResult($"API 连接失败：{ex.Message}", 400);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "API 可用性验证未知异常：{BaseUrl}", baseUrl);
                return new ApiResult($"API 验证异常：{ex.Message}", 400);
            }
        }
    }

    // ===== 私有辅助类型 =====

    /// <summary>OpenAI 兼容格式 /models 响应。</summary>
    file record OpenAiModelsResponse(
        [property: JsonPropertyName("data")] List<OpenAiModelItem> Data);

    file record OpenAiModelItem(
        [property: JsonPropertyName("id")] string Id);
}
