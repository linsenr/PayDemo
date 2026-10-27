using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Infrastructure.Payments.Security;

namespace PaymentStrategyDemo.Infrastructure.Payments.Strategies;

public class WechatPaymentStrategy(IPaymentCallbackVerifier callbackVerifier) : IPayment
{
    public string Type => "wechat";
    public string Name => "微信支付";
    public IReadOnlyCollection<string> SupportedCurrencies { get; } = ["CNY"];

    public Task<PaymentInitiationResult> InitiateAsync(
        PaymentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine("向微信支付发起支付。");
        return Task.FromResult(new PaymentInitiationResult($"WECHAT-{request.OrderId}", "https://example.invalid/pay"));
    }

    public Task<bool> VerifyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default) =>
        callbackVerifier.VerifyAsync(callback, cancellationToken);
}



