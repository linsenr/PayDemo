namespace PaymentStrategyDemo;

// 旧版工厂实现暂时保留为注释，当前支付流程由 PaymentRouter 进行规则路由。
// 后续确认不再需要工厂模式时，再移除此代码。
/*
public class PaymentFactory
{
    private readonly IReadOnlyDictionary<string, IPayment> _payments;

    public PaymentFactory(IEnumerable<IPayment> payments)
    {
        _payments = payments.ToList().ToDictionary(
            payment => payment.Type,
            StringComparer.OrdinalIgnoreCase);
    }

    public IPayment Get(string type)
    {
        if (_payments.TryGetValue(type, out var payment))
        {
            return payment;
        }

        throw new NotSupportedException($"不支持的支付方式：{type}");
    }
}
*/
