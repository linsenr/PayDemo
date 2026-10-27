using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Infrastructure.Payments.Simulation;
using PaymentStrategyDemo.Infrastructure.Payments.Strategies;

namespace PaymentStrategyDemo.Infrastructure.DependencyInjection;

/// <summary>封装微信渠道及其观测和模拟装饰器的注册。</summary>
public static class WechatPaymentModule
{
    public static IServiceCollection AddWechatPaymentChannel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<WechatPaymentStrategy>();
        services.AddSingleton<IPayment>(provider =>
        {
            var wechat = provider.GetRequiredService<WechatPaymentStrategy>();
            var options = provider.GetRequiredService<IOptions<PaymentSimulationOptions>>().Value;
            var simulated = new SimulatedPaymentDecorator(wechat, options);
            return new PaymentObservabilityDecorator(simulated);
        });

        return services;
    }
}
