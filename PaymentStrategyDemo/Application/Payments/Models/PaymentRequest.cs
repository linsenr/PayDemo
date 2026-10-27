namespace PaymentStrategyDemo.Application.Payments.Models;

/// <summary>
/// 支付请求中的路由输入。PreferredPaymentType 为空时由路由规则决定渠道。
/// </summary>
public sealed record PaymentRequest(
    string OrderId,
    decimal Amount,
    string Currency,
    string Region,
    string OrderType,
    string? PreferredPaymentType = null);
