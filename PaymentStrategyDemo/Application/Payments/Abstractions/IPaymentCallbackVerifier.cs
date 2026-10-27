using PaymentStrategyDemo.Application.Payments.Models;

namespace PaymentStrategyDemo.Application.Payments.Abstractions;

public interface IPaymentCallbackVerifier
{
    Task<bool> VerifyAsync(PaymentCallback callback, CancellationToken cancellationToken = default);
}
