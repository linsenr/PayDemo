using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Domain.Payments;
using PaymentStrategyDemo.Infrastructure.Payments.Security;

namespace PaymentStrategyDemo.Infrastructure.Payments.Strategies;

public class AlipayPaymentStrategy(IPaymentCallbackVerifier callbackVerifier) : IPayment, IPaymentQuery
{
    public string Type => "alipay";
    public string Name => "支付宝";
    public IReadOnlyCollection<string> SupportedCurrencies { get; } = ["CNY", "USD"];

    public Task<PaymentInitiationResult> InitiateAsync(
        PaymentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine("向支付宝发起支付。");
        return Task.FromResult(new PaymentInitiationResult($"ALIPAY-{request.OrderId}", null));
    }

    public Task<bool> VerifyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default) =>
        callbackVerifier.VerifyAsync(callback, cancellationToken);

    public Task<PaymentStatus> QueryStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerTransactionId);

        // 示例渠道只模拟查询；真实实现应调用渠道查询 API 并映射其返回状态。
        Console.WriteLine("向支付宝查询支付状态。");
        return Task.FromResult(PaymentStatus.Pending);
    }
}



