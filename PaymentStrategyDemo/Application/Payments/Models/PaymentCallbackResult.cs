using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Application.Payments.Models;

public sealed record PaymentCallbackResult(bool Processed, PaymentOrder Order);
