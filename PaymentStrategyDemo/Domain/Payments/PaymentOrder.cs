namespace PaymentStrategyDemo.Domain.Payments;

public enum PaymentStatus
{
    Created,
    Processing,
    Pending,
    Succeeded,
    Failed,
    Expired
}

public sealed record PaymentOrder(
    string OrderId,
    decimal Amount,
    string Currency,
    string PaymentType,
    PaymentStatus Status,
    string? ProviderTransactionId,
    string? NextActionUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
