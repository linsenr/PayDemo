namespace PaymentStrategyDemo.Application.Payments.Models;

/// <summary>渠道回调的规范化数据；原始报文和签名由渠道适配器负责验证。</summary>
public sealed record PaymentCallback(
    string PaymentType,
    string OrderId,
    string ProviderTransactionId,
    string EventId,
    PaymentCallbackStatus Status,
    string RawPayload,
    string Signature);
