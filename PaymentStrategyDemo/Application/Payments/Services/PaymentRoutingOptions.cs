namespace PaymentStrategyDemo.Application.Payments.Services;

/// <summary>
/// 路由优先级和渠道启用状态。
/// </summary>
public sealed class PaymentRoutingOptions
{
    public decimal WechatSmallOrderThreshold { get; set; } = 50m;

    public HashSet<string> DisabledPaymentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> PreferredPaymentTypesByCurrency { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CNY"] = ["wechat", "alipay"],
            ["USD"] = ["alipay"]
        };
}

