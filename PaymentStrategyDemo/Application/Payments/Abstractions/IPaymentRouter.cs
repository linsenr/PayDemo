using PaymentStrategyDemo.Application.Payments.Models;

namespace PaymentStrategyDemo.Application.Payments.Abstractions;

public interface IPaymentRouter
{
    IPayment Route(PaymentRequest request);
    IPayment GetRegistered(string paymentType);
}
