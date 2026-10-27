using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Infrastructure.Payments.Simulation;

/// <summary>包装支持查询能力的模拟渠道，同时保留可选能力接口。</summary>
public sealed class SimulatedPaymentQueryDecorator : SimulatedPaymentDecorator, IPaymentQuery
{
    private readonly IPaymentQuery _query;

    public SimulatedPaymentQueryDecorator(
        IPayment payment,
        IPaymentQuery query,
        PaymentSimulationOptions options)
        : base(payment, options)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public Task<PaymentStatus> QueryStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default) =>
        _query.QueryStatusAsync(providerTransactionId, cancellationToken);
}
