namespace PaymentStrategyDemo.Infrastructure.Payments.Simulation;

/// <summary>可配置的支付发起异常场景，仅用于本地演示。</summary>
public enum PaymentSimulationMode
{
    None,
    Decline,
    Timeout
}
