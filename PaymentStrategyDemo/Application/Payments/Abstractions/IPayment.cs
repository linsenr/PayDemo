using PaymentStrategyDemo.Application.Payments.Models;

namespace PaymentStrategyDemo.Application.Payments.Abstractions;

public interface IPayment
{
    string Type { get; }
    string Name { get; }
    IReadOnlyCollection<string> SupportedCurrencies { get; }

    Task<PaymentInitiationResult> InitiateAsync(
        PaymentRequest request, CancellationToken cancellationToken = default);

    Task<bool> VerifyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default);
}
