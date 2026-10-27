using Microsoft.Extensions.DependencyInjection;

using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Services;
using PaymentStrategyDemo.Infrastructure.Payments.Persistence;
using PaymentStrategyDemo.Infrastructure.Payments.Security;
using PaymentStrategyDemo.Infrastructure.Payments.Simulation;

namespace PaymentStrategyDemo.Infrastructure.DependencyInjection;

public static class PaymentServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentStrategy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<PaymentSimulationOptions>();
        services.AddAlipayPaymentChannel();
        services.AddWechatPaymentChannel();
        services.AddSingleton<IPaymentRouter, PaymentRouter>();
        services.AddSingleton<IPaymentCallbackVerifier, HmacPaymentCallbackVerifier>();
        services.AddSingleton<IPaymentOrderStore, InMemoryPaymentOrderStore>();
        return services;
    }
}

