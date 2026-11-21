using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using PixelPay.Application.Common;
using PixelPay.Application.Notifications;
using PixelPay.Application.Orders;
using PixelPay.Application.Payments;

namespace PixelPay.IntegrationTests.Support;

public sealed class RecordingJobQueue : IJobQueue
{
    private readonly ConcurrentQueue<Func<IServiceProvider, Task>> _jobs = new();

    public ConcurrentQueue<string> Log { get; } = new();

    public void EnqueueTopup(string orderId, string userId, string gameProductId)
    {
        Log.Enqueue($"topup:{orderId}");
        _jobs.Enqueue(sp => sp.GetRequiredService<TopupJob>().RunAsync(orderId, userId, gameProductId, CancellationToken.None));
    }

    public void SchedulePaymentPoll(string paymentId, string chargeId, int attempt, TimeSpan delay)
    {
        Log.Enqueue($"poll:{paymentId}:{attempt}");
        _jobs.Enqueue(sp => sp.GetRequiredService<PaymentPollJob>().RunAsync(paymentId, chargeId, attempt, CancellationToken.None));
    }

    public void EnqueueNotification(string notificationId)
    {
        Log.Enqueue($"notification:{notificationId}");
        _jobs.Enqueue(sp => sp.GetRequiredService<NotificationDispatchJob>().RunAsync(notificationId, CancellationToken.None));
    }

    public bool TryDequeue(out Func<IServiceProvider, Task> job) => _jobs.TryDequeue(out job!);

    public void Clear()
    {
        _jobs.Clear();
        Log.Clear();
    }
}

/// <summary>Every provider slug resolves to one scripted adapter; tests set <see cref="Next"/> per order.</summary>
public sealed class FakeTopupProviders : ITopupProviderFactory, ITopupProvider
{
    public Func<TopupRequest, TopupResult> Next { get; set; } = _ => new TopupResult(true, "prov-1", "ref-1");

    public ConcurrentQueue<TopupRequest> Calls { get; } = new();

    public string Slug => "fake";

    public ITopupProvider Create(ProviderCredentials provider) => this;

    public Task<TopupResult> ProcessTopupAsync(TopupRequest request, CancellationToken ct)
    {
        Calls.Enqueue(request);
        return Task.FromResult(Next(request));
    }
}

public sealed class FakePaymentGateway : IPaymentGateway
{
    public ConcurrentDictionary<string, bool> Paid { get; } = new();

    public bool FailCreate { get; set; }

    private int _seq;

    public Task<QrCharge> CreatePromptPayQrAsync(decimal amount, CancellationToken ct)
    {
        if (FailCreate)
            throw new HttpRequestException("gateway down");
        var id = $"REF{Interlocked.Increment(ref _seq):D12}";
        return Task.FromResult(new QrCharge(id, "data:image/png;base64,AAAA", DateTime.UtcNow.AddMinutes(15), amount));
    }

    public Task<bool> IsPaidAsync(string chargeId, CancellationToken ct) => Task.FromResult(Paid.GetValueOrDefault(chargeId));
}

public sealed class RecordingOrderNotifier : IOrderStatusNotifier
{
    public ConcurrentQueue<(string UserId, string OrderId, string Status)> Sent { get; } = new();

    public Task NotifyAsync(string userId, string orderId, string status, CancellationToken ct = default)
    {
        Sent.Enqueue((userId, orderId, status));
        return Task.CompletedTask;
    }
}

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string html, CancellationToken ct) => Task.CompletedTask;
}
