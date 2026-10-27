using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Application.Payments.Abstractions;

/// <summary>可选能力：查询支付渠道中的交易状态。</summary>
public interface IPaymentQuery
{
    Task<PaymentStatus> QueryStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default);
}
