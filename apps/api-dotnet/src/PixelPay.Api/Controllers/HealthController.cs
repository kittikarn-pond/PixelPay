using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace PixelPay.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController(MySqlDataSource db) : ControllerBase
{
    [HttpGet]
    public object Check() => Wrap(new
    {
        status = "ok",
        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
    });

    [HttpGet("db")]
    public async Task<object> CheckDb(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new MySqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(ct);
        return Wrap(new { status = "ok", database = "connected" });
    }

    // Stand-in until the global envelope filter (Task 3) wraps every response.
    private static object Wrap(object data) => new { success = true, data };
}
