using System.Text.Encodings.Web;
using System.Text.Unicode;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Scalar.AspNetCore;

using Serilog;
using Serilog.Events;

using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Application;
using SpeakEase.Write.HealthChecks;
using SpeakEase.Write.Infrastructure.AI;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.JsonConverters;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.MapRoute.AI;
using SpeakEase.Write.MapRoute.Auth;
using SpeakEase.Write.MapRoute.Dashboard;
using SpeakEase.Write.MapRoute.Novel;
using SpeakEase.Write.MapRoute.References;
using SpeakEase.Write.MapRoute.Tags;
using SpeakEase.Write.MapRoute.Users;
using SpeakEase.Write.MapRoute.Works;
using SpeakEase.Write.Middleware;

var logPath = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SpeakEase.Write")
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Debug)
        .WriteTo.File(
            Path.Combine(logPath, "debug", "debug-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
        .WriteTo.File(
            Path.Combine(logPath, "info", "info-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
        .WriteTo.File(
            Path.Combine(logPath, "warning", "warning-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error || e.Level == LogEventLevel.Fatal)
        .WriteTo.File(
            Path.Combine(logPath, "error", "error-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("SpeakEase.Write 启动中...");

    var builder = WebApplication.CreateSlimBuilder();

    if (!builder.Environment.IsDevelopment())
    {
        ValidateProductionConfiguration(builder.Configuration);
    }

    builder.Host.UseSerilog();

    // ── 基础服务 ──
    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    });
    builder.Services.AddOpenApi();

    // ── CORS ──
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                    builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromHours(24));
        });
    });

    // ── 响应压缩 ──
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    // ── 健康检查 ──
    builder.Services.AddHealthChecks()
        .AddCheck<DbContextHealthCheck>("database", tags: new[] { "ready" });

    // ── 持久化 + 缓存 + 认证 ──
    builder.Services.AddInfrastructurePersistence(builder.Configuration);
    builder.Services.ConfigureHttpJsonOptions(op =>
    {
        op.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        op.SerializerOptions.Converters.Add(new DateTimeConverter());
        op.SerializerOptions.Converters.Add(new DateTimeNullConverter());
        op.SerializerOptions.Converters.Add(new LongConverter());
        op.SerializerOptions.Converters.Add(new LongNullConverter());
    });
    builder.Services.AddMutilCache(builder.Configuration);
    builder.Services.AddJwt(builder.Configuration);

    // ── Application 层 ──
    builder.Services.AddApplicationServices();

    // ── AI Lib + Novel AI ──
    builder.Services.AddChatLLM();
    builder.Services.AddNovelAI();
    builder.Services.AddScoped<IOpenAIContext, OpenAIContext>();

    var app = builder.Build();

    // ── 中间件管道 ──

    // 全局异常处理（最外层，兜底所有未处理异常）
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // 慢请求告警
    app.UseMiddleware<RequestTimingMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();

    app.UseResponseCompression();
    app.UseMiddleware<RateLimitMiddleware>();

    // 请求日志（Serilog）
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
        app.MapOpenApi();
    }

    // ── 健康检查 ──
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    // ── 路由端点 ──
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapLLMLogEndPoint();
    app.MapLLMEndPoint();
    app.MapAuthEndPoint();
    app.MapUserEndPoint();
    app.MapModelEndPoint();
    app.MapWorkEndPoint();
    app.MapStoryEndPoint();
    app.MapVolumeEndPoint();
    app.MapForeshadowingEndPoint();
    app.MapTimelineEndPoint();
    app.MapWorldEndPoint();
    app.MapRelationshipEndPoint();
    app.MapCharacterGraphEndPoint();
    app.MapCharacterArcEndPoint();
    app.MapAllCharacterArcsEndPoint();
    app.MapInspirationEndPoint();
    app.MapReferenceEndPoint();
    app.MapTagEndPoint();
    app.MapDashboardEndPoint();
    app.MapAgentEndPoint();
    app.MapSessionEndPoint();
    app.MapAdoptionEndPoint();
    app.MapVersionEndPoint();
    app.MapAutoSaveEndPoint();
    app.MapExportEndPoint();

    Log.Information("SpeakEase.Write 已启动");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
}
finally
{
    Log.CloseAndFlush();
}

static void ValidateProductionConfiguration(IConfiguration configuration)
{
    var jwtSecret = configuration["JwtOptions:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecret) ||
        jwtSecret.Contains("SuperSecret", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Production JWT secret must be supplied from a secure secret store.");
    }

    var connectionString = configuration.GetConnectionString("SpeakEaseWrite");
    if (string.IsNullOrWhiteSpace(connectionString) ||
        connectionString.Contains("Password=blog123", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Production database connection string must be supplied from a secure secret store.");
    }
}
