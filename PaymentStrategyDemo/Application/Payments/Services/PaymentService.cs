using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Application.Payments.Services;

public sealed class PaymentService
{
    private readonly IPaymentRouter _router;
    private readonly IPaymentOrderStore _orders;

    public PaymentService(IPaymentRouter router, IPaymentOrderStore orders)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    public async Task<PaymentOrder> CreatePaymentAsync(
        PaymentRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = request with
        {
            OrderId = request.OrderId.Trim(),
            Currency = request.Currency.ToUpperInvariant(),
            Region = request.Region.Trim(),
            OrderType = request.OrderType.Trim(),
            PreferredPaymentType = request.PreferredPaymentType?.Trim()
        };
        var payment = _router.Route(normalizedRequest);
        var now = DateTimeOffset.UtcNow;
        var order = new PaymentOrder(
            normalizedRequest.OrderId, normalizedRequest.Amount, normalizedRequest.Currency,
            payment.Type, PaymentStatus.Created, null, null, now, now);

        // 先记录本地支付单，确保渠道调用前已有可追踪记录。
        await _orders.CreateAsync(order, cancellationToken);
        await _orders.MarkProcessingAsync(order.OrderId, cancellationToken);

        // 渠道异常不应直接标记失败：超时可能意味着渠道已成功受理，需查询或对账。
        var result = await payment.InitiateAsync(normalizedRequest, cancellationToken);
        ValidateInitiationResult(result);
        return await _orders.MarkInitiationResultAsync(order.OrderId, result, cancellationToken);
    }

    public async Task<PaymentCallbackResult> HandleCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default)
    {
        ValidateCallback(callback);
        cancellationToken.ThrowIfCancellationRequested();

        var order = await _orders.GetAsync(callback.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("找不到对应的支付记录。");
        if (!string.Equals(order.PaymentType, callback.PaymentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("回调支付渠道与本地支付记录不匹配。");
        }

        var payment = _router.GetRegistered(order.PaymentType);
        if (!await payment.VerifyCallbackAsync(callback, cancellationToken))
        {
            throw new InvalidOperationException("支付渠道回调验证失败。");
        }

        return await _orders.ApplyCallbackAsync(callback, cancellationToken);
    }

    public Task<PaymentOrder?> GetPaymentAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("订单号不能为空。", nameof(orderId));
        }

        return _orders.GetAsync(orderId.Trim(), cancellationToken);
    }

    /// <summary>仅对实现查询能力的渠道发起主动查询，并通过仓储的领域状态机应用结果。</summary>
    public async Task<PaymentOrder> QueryPaymentStatusAsync(
        string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId) || orderId.Length > 128)
        {
            throw new ArgumentException("订单号不能为空且不能超过 128 个字符。", nameof(orderId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var order = await _orders.GetAsync(orderId.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException("找不到对应的支付记录。");
        if (string.IsNullOrWhiteSpace(order.ProviderTransactionId))
        {
            throw new InvalidOperationException("支付渠道尚未返回交易号，无法查询支付状态。");
        }

        var payment = _router.GetRegistered(order.PaymentType);
        if (payment is not IPaymentQuery queryCapability)
        {
            throw new NotSupportedException($"支付渠道 {payment.Name} 不支持主动查询。");
        }

        var status = await queryCapability.QueryStatusAsync(order.ProviderTransactionId, cancellationToken);
        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException("支付渠道返回了无效的支付状态。");
        }

        return await _orders.ApplyStatusUpdateAsync(order.OrderId, status, cancellationToken);
    }

    private static void ValidateRequest(PaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new ArgumentException("订单号不能为空。", nameof(request));
        if (request.OrderId.Length > 128)
            throw new ArgumentException("订单号不能超过 128 个字符。", nameof(request));
        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "支付金额必须大于 0。");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3
            || request.Currency.Any(character => !char.IsAsciiLetter(character)))
            throw new ArgumentException("币种必须是 3 位字母代码。", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Region))
            throw new ArgumentException("地区不能为空。", nameof(request));
        if (string.IsNullOrWhiteSpace(request.OrderType))
            throw new ArgumentException("订单类型不能为空。", nameof(request));
        if (request.PreferredPaymentType is not null && string.IsNullOrWhiteSpace(request.PreferredPaymentType))
            throw new ArgumentException("首选支付渠道不能是空白值。", nameof(request));
    }

    private static void ValidateInitiationResult(PaymentInitiationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(result.ProviderTransactionId))
            throw new InvalidOperationException("支付渠道未返回有效交易号。");
        if (result.ProviderTransactionId.Length > 256)
            throw new InvalidOperationException("支付渠道交易号超出长度限制。");
        if (result.NextActionUrl is not null
            && (!Uri.TryCreate(result.NextActionUrl, UriKind.Absolute, out var actionUri)
                || actionUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(actionUri.UserInfo)))
            throw new InvalidOperationException("支付渠道返回了无效的后续操作地址。");
        if (result.Status is not (PaymentStatus.Pending or PaymentStatus.Processing
            or PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Expired))
            throw new InvalidOperationException("支付渠道返回了非法支付状态。");
    }

    private static void ValidateCallback(PaymentCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (string.IsNullOrWhiteSpace(callback.PaymentType)
            || string.IsNullOrWhiteSpace(callback.OrderId)
            || string.IsNullOrWhiteSpace(callback.ProviderTransactionId)
            || string.IsNullOrWhiteSpace(callback.EventId)
            || string.IsNullOrWhiteSpace(callback.RawPayload)
            || string.IsNullOrWhiteSpace(callback.Signature))
            throw new ArgumentException("支付回调缺少必需字段。", nameof(callback));
        if (callback.EventId.Length > 128 || callback.OrderId.Length > 128
            || callback.PaymentType.Length > 64 || callback.ProviderTransactionId.Length > 256
            || callback.Signature.Length > 1024 || callback.RawPayload.Length > 1_000_000)
            throw new ArgumentException("支付回调标识超出长度限制。", nameof(callback));
        if (!Enum.IsDefined(callback.Status))
            throw new ArgumentException("支付回调状态无效。", nameof(callback));
    }
}

