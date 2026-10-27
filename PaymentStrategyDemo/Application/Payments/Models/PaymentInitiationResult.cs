using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Application.Payments.Models;

public sealed record PaymentInitiationResult(
    string ProviderTransactionId,
    string? NextActionUrl,
    PaymentStatus Status = PaymentStatus.Pending);
