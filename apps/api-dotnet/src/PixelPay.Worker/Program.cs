using Hangfire;
using PixelPay.Infrastructure;
using PixelPay.Infrastructure.Config;
using PixelPay.Infrastructure.Jobs;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// Background jobs: top-ups, PromptPay polling and notification delivery.
// WORKER_QUEUES picks the queues (comma-separated, default all), so prod can run separate top-up and notification workers.
var builder = WebApplication.CreateBuilder(args);
var env = EnvConfig.Load(builder.Configuration);

builder.WebHost.UseUrls($"http://0.0.0.0:{builder.Configuration["WORKER_PORT"] ?? "3100"}");
builder.Services.AddSerilog(lc => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddPixelPay(builder.Configuration, env);

var queues = (builder.Configuration["WORKER_QUEUES"] ?? string.Join(',', Queues.All))
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var unknown = queues.Except(Queues.All).ToArray();
if (unknown.Length > 0)
    throw new InvalidOperationException($"Unknown WORKER_QUEUES: {string.Join(", ", unknown)}");

builder.Services.AddHangfireServer(o =>
{
    o.Queues = queues;
    o.WorkerCount = int.TryParse(builder.Configuration["WORKER_CONCURRENCY"], out var n) ? n : Environment.ProcessorCount * 5;
    o.ServerName = $"{Environment.MachineName}:{string.Join('+', queues)}";
});

var app = builder.Build();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", queues }));
Log.Information("Worker started for queues {Queues}", queues);
app.Run();
