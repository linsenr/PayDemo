using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Application.Payments.Abstractions;

public interface IPaymentOrderStore
{
    Task<PaymentOrder> CreateAsync(PaymentOrder order, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> GetAsync(string orderId, CancellationToken cancellationToken = default);
    Task<PaymentOrder> MarkProcessingAsync(string orderId, CancellationToken cancellationToken = default);
    Task<PaymentOrder> MarkInitiationResultAsync(
        string orderId, PaymentInitiationResult result, CancellationToken cancellationToken = default);
    Task<PaymentOrder> ApplyStatusUpdateAsync(
        string orderId, PaymentStatus status, CancellationToken cancellationToken = default);
    Task<PaymentCallbackResult> ApplyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default);
}
