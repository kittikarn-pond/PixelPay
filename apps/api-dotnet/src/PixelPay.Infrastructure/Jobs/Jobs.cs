using Hangfire;
using PixelPay.Application.Common;
using PixelPay.Application.Notifications;
using PixelPay.Application.Orders;
using PixelPay.Application.Payments;

namespace PixelPay.Infrastructure.Jobs;

public static class Queues
{
    public const string Topup = "topup";
    public const string Payments = "payments";
    public const string Notifications = "notifications";

    public static readonly string[] All = [Topup, Payments, Notifications];
}

public sealed class HangfireJobQueue(IBackgroundJobClient client) : IJobQueue
{
    public void EnqueueTopup(string orderId, string userId, string gameProductId) =>
        client.Enqueue<TopupRunner>(r => r.RunAsync(orderId, userId, gameProductId, CancellationToken.None));

    public void SchedulePaymentPoll(string paymentId, string chargeId, int attempt, TimeSpan delay) =>
        client.Schedule<PaymentPollRunner>(r => r.RunAsync(paymentId, chargeId, attempt, CancellationToken.None), delay);

    public void EnqueueNotification(string notificationId) =>
        client.Enqueue<NotificationRunner>(r => r.RunAsync(notificationId, CancellationToken.None));
}

// Hangfire entry points: the queue and retry policy live here, the logic in PixelPay.Application.
// Hangfire swaps the CancellationToken argument for the server's shutdown token.

public sealed class TopupRunner(TopupJob job)
{
    // The job handles provider failures itself; a retry after a crash is safe because the order is claimed from
    // PENDING first, so a half-done run is not repeated.
    [Queue(Queues.Topup)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [5, 10])]
    public Task RunAsync(string orderId, string userId, string gameProductId, CancellationToken ct) =>
        job.RunAsync(orderId, userId, gameProductId, ct);
}

public sealed class PaymentPollRunner(PaymentPollJob job)
{
    // Each attempt schedules the next one itself; a crashed attempt is retried a few times before giving up.
    [Queue(Queues.Payments)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [10, 10, 10])]
    public Task RunAsync(string paymentId, string chargeId, int attempt, CancellationToken ct) =>
        job.RunAsync(paymentId, chargeId, attempt, ct);
}

public sealed class NotificationRunner(NotificationDispatchJob job)
{
    [Queue(Queues.Notifications)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [3, 6])]
    public Task RunAsync(string notificationId, CancellationToken ct) => job.RunAsync(notificationId, ct);
}
