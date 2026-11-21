using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Api.Envelope;
using PixelPay.Api.Json;
using PixelPay.Api.Routing;
using PixelPay.Api.Validation;
using PixelPay.Application.Common;
using PixelPay.Infrastructure.Config;
using PixelPay.Infrastructure;
using PixelPay.Infrastructure.Persistence;
using PixelPay.Infrastructure.Seed;
using PixelPay.Infrastructure.Realtime;
using PixelPay.Api.Controllers;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
var env = EnvConfig.Load(builder.Configuration);

builder.WebHost.UseUrls($"http://0.0.0.0:{env.Port}");

builder.Services.AddSerilog(lc => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddPixelPay(builder.Configuration, env);
builder.Services.AddJwtAuth(env);
builder.Services.AddScoped<AuditFilter>();

builder.Services.AddControllers(o =>
    {
        o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        o.ModelBindingMessageProvider.SetMissingRequestBodyRequiredValueAccessor(() => NestErrors.InvalidJsonMessage);
        o.Filters.Add<NonJsonBodyFilter>();
        o.Filters.Add<StrictBodyFilter>();
        o.Filters.Add<ValidationFilter>();
        o.Filters.Add<EnvelopeResultFilter>();
    })
    .AddJsonOptions(o =>
    {
        ApiJson.Apply(o.JsonSerializerOptions);
        o.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.SuppressMapClientErrors = true;
        o.InvalidModelStateResponseFactory = ModelStateErrors.ToNestError;
    });
builder.Services.AddValidatorsFromAssemblyContaining<ApiException>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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

// One-shot commands: `dotnet PixelPay.Api.dll migrate` (run before the API starts) and `... seed` (storefront catalog).
if (args is ["migrate" or "seed", ..])
{
    using var scope = app.Services.CreateScope();
    if (args[0] == "migrate")
        await scope.ServiceProvider.GetRequiredService<DbMigrator>().MigrateAsync(CancellationToken.None);
    else
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync(CancellationToken.None);
    return;
}

app.UseSerilogRequestLogging();
app.UseMiddleware<HeadAsGetMiddleware>();
app.UseMiddleware<ExceptionEnvelopeMiddleware>();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<OrdersHub>(OrdersHub.Path);

app.Run();

public partial class Program;
