using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Infrastructure.Payments.Simulation;

/// <summary>在不修改真实渠道策略的前提下，注入可重复的支付发起失败场景。</summary>
public class SimulatedPaymentDecorator : IPayment
{
    protected IPayment Inner { get; }
    private readonly PaymentSimulationOptions _options;

    public SimulatedPaymentDecorator(IPayment inner, PaymentSimulationOptions options)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (!Enum.IsDefined(options.InitiationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "支付模拟模式无效。");
        }

        if (string.IsNullOrWhiteSpace(options.TargetPaymentType)
            || options.TargetPaymentType.Length > 64)
        {
            throw new ArgumentException("模拟目标渠道不能为空且不能超过 64 个字符。", nameof(options));
        }

        if (options.TimeoutDelayMilliseconds is < 1 or > 60_000)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "模拟超时时间必须在 1 到 60000 毫秒之间。");
        }
    }

    public string Type => Inner.Type;
    public string Name => Inner.Name;
    public IReadOnlyCollection<string> SupportedCurrencies => Inner.SupportedCurrencies;

    public async Task<PaymentInitiationResult> InitiateAsync(
        PaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (!Inner.Type.Equals(_options.TargetPaymentType, StringComparison.OrdinalIgnoreCase))
        {
            return await Inner.InitiateAsync(request, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _options.InitiationMode switch
        {
            PaymentSimulationMode.None => await Inner.InitiateAsync(request, cancellationToken),
            PaymentSimulationMode.Decline => SimulateDecline(request),
            PaymentSimulationMode.Timeout => await SimulateTimeoutAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(_options), "支付模拟模式无效。")
        };
    }

    public Task<bool> VerifyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default) =>
        Inner.VerifyCallbackAsync(callback, cancellationToken);

    private PaymentInitiationResult SimulateDecline(PaymentRequest request) => new(
        $"SIM-{Inner.Type.ToUpperInvariant()}-{request.OrderId}",
        null,
        PaymentStatus.Failed);

    private async Task<PaymentInitiationResult> SimulateTimeoutAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(_options.TimeoutDelayMilliseconds, cancellationToken);
        throw new TimeoutException($"模拟支付渠道 {Inner.Name} 调用超时。");
    }
}
