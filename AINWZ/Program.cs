using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.Auth;
using SpeakEase.Write.Application.Contracts.Dashboard;
using SpeakEase.Write.Application.Contracts.References;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Tags;
using SpeakEase.Write.Application.Contracts.Users;
using SpeakEase.Write.Application.Contracts.Works;
using SpeakEase.Write.Infrastructure.AI;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.JsonConverters;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.MapRoute.AI;
using SpeakEase.Write.MapRoute.Auth;
using SpeakEase.Write.MapRoute.Dashboard;
using SpeakEase.Write.MapRoute.References;
using SpeakEase.Write.MapRoute.Tags;
using SpeakEase.Write.MapRoute.Users;
using SpeakEase.Write.MapRoute.Works;


var logPath = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SpeakEase.Blog")
    // 全量日志（Debug 级别及以上）
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Debug)
        .WriteTo.File(
            Path.Combine(logPath, "debug", "debug-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    // 信息日志
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
        .WriteTo.File(
            Path.Combine(logPath, "info", "info-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    // 警告日志
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
        .WriteTo.File(
            Path.Combine(logPath, "warning", "warning-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    // 错误日志
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error || e.Level == LogEventLevel.Fatal)
        .WriteTo.File(
            Path.Combine(logPath, "error", "error-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    // 控制台输出
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("SpeakEase.Write 启动中...");

    var builder = WebApplication.CreateSlimBuilder();

    builder.Host.UseSerilog();

    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthorization();
    builder.Services.AddOpenApi();
    builder.Services.AddInfrastructurePersistence(builder.Configuration);
    builder.Services.ConfigureHttpJsonOptions(op =>
    {
        op.SerializerOptions.Converters.Add(new DateTimeConverter());
        op.SerializerOptions.Converters.Add(new DateTimeNullConverter());
        op.SerializerOptions.Converters.Add(new LongConverter());
        op.SerializerOptions.Converters.Add(new LongNullConverter());
    });
    builder.Services.AddMutilCache(builder.Configuration);
    builder.Services.AddJwt(builder.Configuration);
    builder.Services.AddScoped<ILLMCallLogApplication,LLMCallLogApplication>();
    builder.Services.AddScoped<IAuthApplication, AuthApplication>();
    builder.Services.AddScoped<IUserApplication, UserApplication>();
    builder.Services.AddScoped<IModelApplication, ModelApplication>();
    builder.Services.AddScoped<IUserModelConfigApplication, UserModelConfigApplication>();
    builder.Services.AddScoped<IWorkApplication, WorkApplication>();
    builder.Services.AddScoped<IChapterApplication, ChapterApplication>();
    builder.Services.AddScoped<ICharacterApplication, CharacterApplication>();
    builder.Services.AddScoped<IOutlineApplication, OutlineApplication>();
    builder.Services.AddScoped<IReferenceApplication, ReferenceApplication>();
    builder.Services.AddScoped<ITagApplication, TagApplication>();
    builder.Services.AddScoped<IDashboardApplication, DashboardApplication>();

    // AI Lib DI: IChatCompatible / IToolCapable / ISkilCapable / IOpenAIContext + 内置工具 KeyedService
    builder.Services.AddChatLLM();

    // 覆盖 IOpenAIContext 默认注册，改为从数据库 + 多级缓存动态解析
    builder.Services.AddScoped<IOpenAIContext, OpenAIContext>();

    var app = builder.Build();


    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference(); // scalar/v1
        app.MapOpenApi();
    }

    // 请求日志（Serilog）
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapLLMLogEndPoint();
    app.MapLLMEndPoint();
    app.MapAuthEndPoint();
    app.MapUserEndPoint();
    app.MapModelEndPoint();
    app.MapAgentEndPoint();
    app.MapWorkEndPoint();
    app.MapStoryEndPoint();
    app.MapReferenceEndPoint();
    app.MapTagEndPoint();
    app.MapDashboardEndPoint();

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


