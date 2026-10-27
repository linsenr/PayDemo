using System.Security.Cryptography;
using System.Text;

using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;

namespace PaymentStrategyDemo.Infrastructure.Payments.Security;

/// <summary>使用支付渠道提供的密钥验证演示回调。密钥仅从环境变量读取。</summary>
public sealed class HmacPaymentCallbackVerifier : IPaymentCallbackVerifier
{
    public Task<bool> VerifyAsync(PaymentCallback callback, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyName = $"PAYMENT_CALLBACK_KEY_{callback.PaymentType.ToUpperInvariant()}";
        var key = Environment.GetEnvironmentVariable(keyName);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(false);
        }

        var expected = CreateSignature(callback, key);
        try
        {
            var expectedBytes = Convert.FromHexString(expected);
            var actualBytes = Convert.FromHexString(callback.Signature);
            return Task.FromResult(CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes));
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }
    }

    public static string CreateSignature(PaymentCallback callback, string key)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var canonicalPayload = string.Join('\n',
            callback.PaymentType,
            callback.OrderId,
            callback.ProviderTransactionId,
            callback.EventId,
            callback.Status,
            callback.RawPayload);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(canonicalPayload));
        return Convert.ToHexString(hash);
    }
}

