using System.Diagnostics;
using System.Diagnostics.Metrics;
using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;

namespace PaymentStrategyDemo.Infrastructure.Payments.Strategies;

/// <summary>为渠道调用统一记录耗时、结果指标和诊断活动，不记录订单或支付凭据。</summary>
public class PaymentObservabilityDecorator : IPayment
{
    private static readonly Meter Meter = new("PaymentStrategyDemo.Payments");
    private static readonly ActivitySource ActivitySource = new("PaymentStrategyDemo.Payments");
    private static readonly Counter<long> Calls = Meter.CreateCounter<long>("payment.channel.calls");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("payment.channel.duration", "ms");

    protected IPayment Inner { get; }

    public PaymentObservabilityDecorator(IPayment inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Type => Inner.Type;
    public string Name => Inner.Name;
    public IReadOnlyCollection<string> SupportedCurrencies => Inner.SupportedCurrencies;

    public Task<PaymentInitiationResult> InitiateAsync(
        PaymentRequest request, CancellationToken cancellationToken = default) =>
        ObserveAsync("initiate", () => Inner.InitiateAsync(request, cancellationToken));

    public Task<bool> VerifyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default) =>
        ObserveAsync("verify_callback", () => Inner.VerifyCallbackAsync(callback, cancellationToken));

    /// <summary>包装异步渠道调用；只使用低敏标签，异常仍交由上层处理。</summary>
    protected async Task<TResult> ObserveAsync<TResult>(string operation, Func<Task<TResult>> action)
    {
        using var activity = ActivitySource.StartActivity($"payment.{operation}");
        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";

        try
        {
            return await action();
        }
        catch
        {
            outcome = "failure";
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { "payment.type", Type },
                { "payment.operation", operation },
                { "payment.outcome", outcome }
            };
            Calls.Add(1, tags);
            Duration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        }
    }
}
