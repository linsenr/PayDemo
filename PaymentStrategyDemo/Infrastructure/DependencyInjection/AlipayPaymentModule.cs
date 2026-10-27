using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Infrastructure.Payments.Simulation;
using PaymentStrategyDemo.Infrastructure.Payments.Strategies;

namespace PaymentStrategyDemo.Infrastructure.DependencyInjection;

/// <summary>封装支付宝渠道及其可选能力、观测和模拟装饰器的注册。</summary>
public static class AlipayPaymentModule
{
    public static IServiceCollection AddAlipayPaymentChannel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AlipayPaymentStrategy>();
        services.AddSingleton<IPayment>(provider =>
        {
            var alipay = provider.GetRequiredService<AlipayPaymentStrategy>();
            var options = provider.GetRequiredService<IOptions<PaymentSimulationOptions>>().Value;
            var simulated = new SimulatedPaymentQueryDecorator(alipay, alipay, options);
            return new PaymentQueryObservabilityDecorator(simulated, simulated);
        });

        return services;
    }
}
