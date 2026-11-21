using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace PixelPay.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController(MySqlDataSource db) : ControllerBase
{
    [HttpGet]
    public object Check() => new { status = "ok", timestamp = DateTime.UtcNow };

    [HttpGet("db")]
    public async Task<object> CheckDb(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new MySqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(ct);
        return new { status = "ok", database = "connected" };
    }
}
