using Microsoft.Extensions.Options;
using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;

namespace PaymentStrategyDemo.Application.Payments.Services;

/// <summary>
/// 按请求条件和已配置的优先级选择一个可用支付渠道。
/// </summary>
public sealed class PaymentRouter : IPaymentRouter
{
    private readonly IReadOnlyDictionary<string, IPayment> _payments;
    private readonly PaymentRoutingOptions _options;

    public PaymentRouter(IEnumerable<IPayment> payments, IOptions<PaymentRoutingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(options);

        var paymentList = payments.ToList();
        if (paymentList.Any(payment => payment is null))
        {
            throw new ArgumentException("支付渠道集合不能包含 null。", nameof(payments));
        }

        foreach (var payment in paymentList)
        {
            if (string.IsNullOrWhiteSpace(payment.Type))
            {
                throw new ArgumentException("支付渠道类型不能为空。", nameof(payments));
            }

            if (payment.SupportedCurrencies is null || payment.SupportedCurrencies.Count == 0
                || payment.SupportedCurrencies.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException($"支付渠道 {payment.Type} 必须声明至少一种有效币种。", nameof(payments));
            }
        }

        var duplicateType = paymentList
            .GroupBy(payment => payment.Type, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateType is not null)
        {
            throw new InvalidOperationException($"支付渠道类型重复注册：{duplicateType.Key}");
        }

        _payments = paymentList.ToDictionary(payment => payment.Type, StringComparer.OrdinalIgnoreCase);
        _options = CopyAndValidateOptions(options.Value, _payments.Keys);
    }

    public IPayment Route(PaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var candidates = GetCandidates(request);
        foreach (var type in candidates)
        {
            if (TryGetAvailablePayment(type, request.Currency, out var payment))
            {
                return payment;
            }
        }

        throw new InvalidOperationException(
            $"没有可用于币种 {request.Currency} 的支付渠道。候选渠道：{string.Join(", ", candidates)}");
    }

    public IPayment GetRegistered(string paymentType)
    {
        if (string.IsNullOrWhiteSpace(paymentType))
        {
            throw new ArgumentException("支付渠道类型不能为空。", nameof(paymentType));
        }

        return _payments.TryGetValue(paymentType.Trim(), out var payment)
            ? payment
            : throw new NotSupportedException($"未注册支付渠道：{paymentType}");
    }

    private IReadOnlyList<string> GetCandidates(PaymentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PreferredPaymentType))
        {
            // 用户明确指定渠道时不自动切换，避免悄悄改变支付选择。
            return [request.PreferredPaymentType.Trim()];
        }

        if (!_options.PreferredPaymentTypesByCurrency.TryGetValue(request.Currency, out var configuredTypes))
        {
            throw new NotSupportedException($"尚未配置币种 {request.Currency} 的支付渠道优先级。");
        }

        var candidates = configuredTypes.ToList();
        if (request.Currency.Equals("CNY", StringComparison.OrdinalIgnoreCase)
            && request.Amount <= _options.WechatSmallOrderThreshold)
        {
            candidates.RemoveAll(type => type.Equals("wechat", StringComparison.OrdinalIgnoreCase));
            candidates.Insert(0, "wechat");
        }

        return candidates;
    }

    private bool TryGetAvailablePayment(string type, string currency, out IPayment payment)
    {
        if (!_options.DisabledPaymentTypes.Contains(type)
            && _payments.TryGetValue(type, out payment!)
            && payment.SupportedCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        payment = null!;
        return false;
    }

    private static PaymentRoutingOptions CopyAndValidateOptions(
        PaymentRoutingOptions options,
        IEnumerable<string> registeredTypes)
    {
        if (options.WechatSmallOrderThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "微信小额订单阈值不能小于 0。");
        }

        if (options.DisabledPaymentTypes is null || options.PreferredPaymentTypesByCurrency is null)
        {
            throw new ArgumentException("禁用渠道和币种优先级配置不能为空。", nameof(options));
        }

        var registeredTypeSet = new HashSet<string>(registeredTypes, StringComparer.OrdinalIgnoreCase);
        var disabledTypes = new HashSet<string>(options.DisabledPaymentTypes, StringComparer.OrdinalIgnoreCase);
        var priorities = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (disabledTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("禁用渠道配置不能包含空类型。", nameof(options));
        }

        foreach (var (currency, configuredTypes) in options.PreferredPaymentTypesByCurrency)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentException("币种优先级配置不能包含空币种。", nameof(options));
            }

            if (configuredTypes is null || configuredTypes.Length == 0
                || configuredTypes.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException($"币种 {currency} 必须配置至少一个有效支付渠道。", nameof(options));
            }

            var types = configuredTypes.Select(type => type.Trim()).ToArray();
            var unknownType = types.FirstOrDefault(type => !registeredTypeSet.Contains(type));
            if (unknownType is not null)
            {
                throw new ArgumentException($"币种 {currency} 配置了未注册渠道：{unknownType}。", nameof(options));
            }

            priorities[currency.Trim()] = types;
        }

        var unknownDisabledType = disabledTypes.FirstOrDefault(type => !registeredTypeSet.Contains(type));
        if (unknownDisabledType is not null)
        {
            throw new ArgumentException($"禁用配置中包含未注册渠道：{unknownDisabledType}。", nameof(options));
        }

        return new PaymentRoutingOptions
        {
            WechatSmallOrderThreshold = options.WechatSmallOrderThreshold,
            DisabledPaymentTypes = disabledTypes,
            PreferredPaymentTypesByCurrency = priorities
        };
    }

    private static void ValidateRequest(PaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            throw new ArgumentException("订单号不能为空。", nameof(request));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "支付金额必须大于 0。");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException("币种不能为空。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Region))
        {
            throw new ArgumentException("地区不能为空。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OrderType))
        {
            throw new ArgumentException("订单类型不能为空。", nameof(request));
        }
    }
}

