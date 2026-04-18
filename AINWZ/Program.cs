using AINWZ.Application.Applications;
using AINWZ.Application.Contracts.AI;
using AINWZ.Application.Contracts.Auth;
using AINWZ.Application.Contracts.Users;
using AINWZ.Infrastructure.Authorization;
using AINWZ.Infrastructure.JsonConverters;
using AINWZ.Infrastructure.LLM;
using AINWZ.Infrastructure.MutilCache;
using AINWZ.Infrastructure.Persistence;
using AINWZ.MapRoute.AI;
using AINWZ.MapRoute.Auth;
using AINWZ.MapRoute.Users;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;


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
    Log.Information("AINWZ 启动中...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthorization();
    builder.Services.AddOpenApi();
    builder.Services.AddInfrastructurePersistence(builder.Configuration);
    builder.Services.AddLLM(builder.Configuration);
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

    Log.Information("AINWZ 已启动");

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


