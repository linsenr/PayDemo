using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Application.Payments.Services;
using PaymentStrategyDemo.Domain.Payments;
using PaymentStrategyDemo.Infrastructure.DependencyInjection;
using PaymentStrategyDemo.Infrastructure.Payments.Security;
using PaymentStrategyDemo.Infrastructure.Payments.Simulation;

Console.WriteLine("支付创建与异步回调示例");

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

using var serviceProvider = new ServiceCollection()
    .AddPaymentStrategy()
    .Configure<PaymentRoutingOptions>(configuration.GetSection("PaymentRouting"))
    .Configure<PaymentSimulationOptions>(configuration.GetSection("PaymentSimulation"))
    .AddScoped<PaymentService>()
    .BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();

var paymentRequest = new PaymentRequest(
    OrderId: "ORDER-2026-001",
    Amount: 30m,
    Currency: "CNY",
    Region: "CN",
    OrderType: "Retail");
PaymentOrder order;
try
{
    order = await paymentService.CreatePaymentAsync(paymentRequest);
}
catch (TimeoutException exception)
{
    Console.WriteLine(exception.Message);
    order = await paymentService.GetPaymentAsync(paymentRequest.OrderId)
        ?? throw new InvalidOperationException("模拟超时后未找到已创建的支付单。");
}

Console.WriteLine($"支付状态：{order.Status}");
if (order.NextActionUrl is not null)
{
    Console.WriteLine($"后续支付操作：{order.NextActionUrl}");
}

var callbackKey = Environment.GetEnvironmentVariable("PAYMENT_CALLBACK_KEY_WECHAT");
if (order.Status is PaymentStatus.Pending or PaymentStatus.Processing
    && !string.IsNullOrWhiteSpace(callbackKey)
    && order.ProviderTransactionId is not null)
{
    var unsignedCallback = new PaymentCallback(
        PaymentType: order.PaymentType,
        OrderId: order.OrderId,
        ProviderTransactionId: order.ProviderTransactionId,
        EventId: "EVENT-2026-001",
        Status: PaymentCallbackStatus.Succeeded,
        RawPayload: "demo-callback-payload",
        Signature: string.Empty);
    var callback = unsignedCallback with
    {
        Signature = HmacPaymentCallbackVerifier.CreateSignature(unsignedCallback, callbackKey)
    };

    var callbackResult = await paymentService.HandleCallbackAsync(callback);
    Console.WriteLine($"回调处理：{(callbackResult.Processed ? "已处理" : "重复或无需处理")}；支付状态：{callbackResult.Order.Status}");

    var simulationOptions = scope.ServiceProvider.GetRequiredService<IOptions<PaymentSimulationOptions>>().Value;
    if (simulationOptions.DuplicateCallbackDelivery)
    {
        var duplicateResult = await paymentService.HandleCallbackAsync(callback);
        Console.WriteLine($"重复回调：{(duplicateResult.Processed ? "已处理" : "幂等忽略")}；支付状态：{duplicateResult.Order.Status}");
    }
}
else
{
    Console.WriteLine("未执行回调：当前状态不适合处理成功通知，或尚未配置回调密钥。");
}
