using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Infrastructure.Payments.Strategies;

/// <summary>保留渠道的可选查询能力，同时为查询调用复用统一观测逻辑。</summary>
public sealed class PaymentQueryObservabilityDecorator : PaymentObservabilityDecorator, IPaymentQuery
{
    private readonly IPaymentQuery _query;

    public PaymentQueryObservabilityDecorator(IPayment payment, IPaymentQuery query)
        : base(payment)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public Task<PaymentStatus> QueryStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default) =>
        ObserveAsync("query_status", () => _query.QueryStatusAsync(providerTransactionId, cancellationToken));
}
