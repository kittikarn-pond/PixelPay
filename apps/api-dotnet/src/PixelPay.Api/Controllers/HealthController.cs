using Hangfire;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PixelPay.Infrastructure.Jobs;

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

    /// <summary>The topup queue's backlog, in BullMQ's getJobCounts shape.</summary>
    [HttpGet("queues")]
    public object CheckQueues([FromServices] JobStorage storage)
    {
        var monitor = storage.GetMonitoringApi();
        var waiting = monitor.EnqueuedCount(Queues.Topup);
        var active = monitor.ProcessingCount();
        var failed = monitor.FailedCount();
        return new { status = "ok", topupQueue = new { waiting, active, failed } };
    }
}
