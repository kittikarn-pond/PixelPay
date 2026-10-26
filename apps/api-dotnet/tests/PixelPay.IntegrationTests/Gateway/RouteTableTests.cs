extern alias gateway;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PixelPay.IntegrationTests.Gateway;

// Runs the gateway with its real appsettings.json, pointing both clusters at stub
// backends that report which one served the request.
public sealed class RouteTableTests : IAsyncLifetime
{
    private WebApplication _node = null!;
    private WebApplication _dotnet = null!;
    private WebApplicationFactory<gateway::Program> _gateway = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _node = await StartStub("node");
        _dotnet = await StartStub("dotnet");
        _gateway = new WebApplicationFactory<gateway::Program>().WithWebHostBuilder(b => b
            .UseSetting("NODE_API_URL", _node.Urls.Single())
            .UseSetting("NET_API_URL", _dotnet.Urls.Single()));
        _client = _gateway.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _gateway.DisposeAsync();
        await _dotnet.DisposeAsync();
        await _node.DisposeAsync();
    }

    [Theory]
    [InlineData("POST", "/api/v1/orders", "node")]
    [InlineData("GET", "/api/v1/orders", "dotnet")]
    [InlineData("HEAD", "/api/v1/orders", "dotnet")]
    [InlineData("GET", "/api/v1/orders?page=2&limit=5", "dotnet")]
    [InlineData("GET", "/api/v1/orders/abc", "dotnet")]
    [InlineData("HEAD", "/api/v1/orders/abc", "dotnet")]
    [InlineData("POST", "/api/v1/orders/abc/cancel", "node")]
    [InlineData("GET", "/api/v1/games", "node")]
    [InlineData("POST", "/api/v1/payments/webhook", "node")]
    [InlineData("GET", "/api/v1/wallet", "dotnet")]
    [InlineData("GET", "/api/v1/wallet/transactions", "dotnet")]
    [InlineData("POST", "/api/v1/wallet/deposit", "dotnet")]
    [InlineData("POST", "/api/v1/auth/login", "dotnet")]
    [InlineData("GET", "/api/v1/auth/me", "dotnet")]
    [InlineData("PATCH", "/api/v1/users/profile", "dotnet")]
    [InlineData("PUT", "/api/v1/users/change-password", "dotnet")]
    [InlineData("GET", "/api/v1/health", "node")]
    [InlineData("GET", "/socket.io/?EIO=4&transport=polling", "node")]
    public async Task Routes_request_to_backend(string method, string pathAndQuery, string backend)
    {
        using var res = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), pathAndQuery), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(backend, Header(res, "X-Backend"));
        Assert.Equal($"{method} {pathAndQuery}", Header(res, "X-Request"));
    }

    [Fact]
    public async Task Passes_cors_headers_through_untouched()
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        req.Headers.Add("Origin", "http://localhost:4001");
        req.Headers.Add("Access-Control-Request-Method", "POST");

        using var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal("dotnet", Header(res, "X-Backend"));
        Assert.Equal("http://localhost:4001", Header(res, "X-Origin"));
        Assert.Equal(["http://stub-origin"], res.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    private static string? Header(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) ? values.Single() : null;

    private static async Task<WebApplication> StartStub(string name)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(ctx =>
        {
            ctx.Response.Headers["X-Backend"] = name;
            ctx.Response.Headers["X-Request"] = $"{ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString}";
            ctx.Response.Headers["X-Origin"] = ctx.Request.Headers.Origin.ToString();
            ctx.Response.Headers.AccessControlAllowOrigin = "http://stub-origin";
            return Task.CompletedTask;
        });
        await app.StartAsync();
        return app;
    }
}
