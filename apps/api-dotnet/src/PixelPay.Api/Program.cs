using System.Text.Json;
using MySqlConnector;
using PixelPay.Infrastructure.Config;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
var env = EnvConfig.Load(builder.Configuration);

builder.WebHost.UseUrls($"http://0.0.0.0:{env.Port}");

builder.Services.AddSerilog(lc => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddSingleton(env);
builder.Services.AddSingleton(_ => new MySqlDataSource(env.ConnectionString));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(env.FrontendUrl)
    .AllowCredentials()
    .AllowAnyHeader()
    .AllowAnyMethod()));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program;
