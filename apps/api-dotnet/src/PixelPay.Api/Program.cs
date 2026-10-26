using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PixelPay.Api.Envelope;
using PixelPay.Api.Json;
using PixelPay.Api.Routing;
using PixelPay.Api.Validation;
using PixelPay.Application.Common;
using PixelPay.Infrastructure.Config;
using PixelPay.Infrastructure.Persistence;
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
builder.Services.AddPersistence(env);

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

app.UseSerilogRequestLogging();
app.UseMiddleware<HeadAsGetMiddleware>();
app.UseMiddleware<ExceptionEnvelopeMiddleware>();
app.UseRouting();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program;
