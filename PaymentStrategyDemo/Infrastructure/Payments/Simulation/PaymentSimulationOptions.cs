namespace PaymentStrategyDemo.Infrastructure.Payments.Simulation;

/// <summary>控制单个渠道的模拟行为；默认不改变正常支付演示流程。</summary>
public sealed class PaymentSimulationOptions
{
    public string TargetPaymentType { get; set; } = "wechat";
    public PaymentSimulationMode InitiationMode { get; set; } = PaymentSimulationMode.None;
    public int TimeoutDelayMilliseconds { get; set; } = 500;
    public bool DuplicateCallbackDelivery { get; set; }
}
