using System.Net;
using System.Text.Json;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests;

public class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_status_and_iso_timestamp()
    {
        var res = await _client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = body.RootElement;
        Assert.Equal(["success", "data"], root.EnumerateObject().Select(p => p.Name));
        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");
        Assert.Equal(["status", "timestamp"], data.EnumerateObject().Select(p => p.Name));
        Assert.Equal("ok", data.GetProperty("status").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", data.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task Health_db_reports_connected()
    {
        var res = await _client.GetAsync("/api/v1/health/db", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("""{"success":true,"data":{"status":"ok","database":"connected"}}""",
            await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Health_is_only_served_under_api_v1()
    {
        var res = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
