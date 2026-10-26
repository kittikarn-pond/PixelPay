using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ReverseProxy:Clusters:node:Destinations:node:Address"] = Required("NODE_API_URL"),
    ["ReverseProxy:Clusters:dotnet:Destinations:dotnet:Address"] = Required("NET_API_URL"),
});

builder.Services.AddSerilog(lc => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapReverseProxy();

app.Run();

string Required(string name) =>
    builder.Configuration[name] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is required");

public partial class Program;
