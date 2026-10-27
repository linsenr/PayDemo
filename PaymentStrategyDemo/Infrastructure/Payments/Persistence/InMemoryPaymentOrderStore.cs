using PaymentStrategyDemo.Application.Payments.Abstractions;
using PaymentStrategyDemo.Application.Payments.Models;
using PaymentStrategyDemo.Domain.Payments;

namespace PaymentStrategyDemo.Infrastructure.Payments.Persistence;

/// <summary>
/// 仅用于演示的内存仓储。生产环境应使用具备事务和唯一约束的数据库实现。
/// </summary>
public sealed class InMemoryPaymentOrderStore : IPaymentOrderStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PaymentOrder> _orders = new(StringComparer.Ordinal);
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);

    /// <summary>创建支付单；订单唯一性由仓储负责保证。</summary>
    public Task<PaymentOrder> CreateAsync(PaymentOrder order, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(order);

        lock (_sync)
        {
            if (_orders.ContainsKey(order.OrderId))
            {
                throw new InvalidOperationException($"订单 {order.OrderId} 已创建支付记录。");
            }

            _orders.Add(order.OrderId, order);
            return Task.FromResult(order);
        }
    }

    /// <summary>按订单号读取支付单。</summary>
    public Task<PaymentOrder?> GetAsync(string orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _orders.TryGetValue(orderId, out var order);
            return Task.FromResult(order);
        }
    }

    /// <summary>使用领域状态机推进支付单，仓储只负责保存转换结果。</summary>
    public Task<PaymentOrder> MarkProcessingAsync(string orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var current = GetRequired(orderId);
            var updated = PaymentStateMachine.Transition(
                current, PaymentStatus.Processing, DateTimeOffset.UtcNow);
            _orders[orderId] = updated;
            return Task.FromResult(updated);
        }
    }

    /// <summary>保存渠道发起结果；若回调已先完成，则保留已确认的终态。</summary>
    public Task<PaymentOrder> MarkInitiationResultAsync(
        string orderId, PaymentInitiationResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(result);

        lock (_sync)
        {
            var current = GetRequired(orderId);
            // 渠道回调可能先于同步响应到达；较晚的同步结果不能覆盖已确认的终态。
            if (PaymentStateMachine.IsTerminal(current.Status))
            {
                return Task.FromResult(current);
            }

            var now = DateTimeOffset.UtcNow;
            var transitioned = PaymentStateMachine.Transition(current, result.Status, now);
            var updated = transitioned with
            {
                ProviderTransactionId = result.ProviderTransactionId,
                NextActionUrl = result.NextActionUrl,
                UpdatedAt = now
            };
            _orders[orderId] = updated;
            return Task.FromResult(updated);
        }
    }

    /// <summary>将主动查询等渠道状态结果交给领域状态机校验后保存。</summary>
    public Task<PaymentOrder> ApplyStatusUpdateAsync(
        string orderId, PaymentStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var current = GetRequired(orderId);
            var updated = PaymentStateMachine.Transition(current, status, DateTimeOffset.UtcNow);
            _orders[orderId] = updated;
            return Task.FromResult(updated);
        }
    }

    /// <summary>原子地去重回调并应用状态转换，保证本地示例中的并发通知不会重复推进。</summary>
    public Task<PaymentCallbackResult> ApplyCallbackAsync(
        PaymentCallback callback, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(callback);

        lock (_sync)
        {
            var current = GetRequired(callback.OrderId);
            var eventKey = $"{callback.PaymentType}:{callback.EventId}";
            if (_processedEvents.Contains(eventKey))
            {
                return Task.FromResult(new PaymentCallbackResult(false, current));
            }

            if (!string.Equals(current.PaymentType, callback.PaymentType, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(current.ProviderTransactionId, callback.ProviderTransactionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("回调渠道或渠道交易号与本地支付记录不匹配。");
            }

            var targetStatus = callback.Status switch
            {
                PaymentCallbackStatus.Succeeded => PaymentStatus.Succeeded,
                PaymentCallbackStatus.Failed => PaymentStatus.Failed,
                PaymentCallbackStatus.Expired => PaymentStatus.Expired,
                _ => throw new ArgumentOutOfRangeException(nameof(callback))
            };

            if (current.Status == targetStatus)
            {
                // 不同事件号也可能重复报告成功；记下事件后返回未处理，保持幂等。
                _processedEvents.Add(eventKey);
                return Task.FromResult(new PaymentCallbackResult(false, current));
            }

            var transitioned = PaymentStateMachine.Transition(
                current, targetStatus, DateTimeOffset.UtcNow);
            _orders[callback.OrderId] = transitioned;
            _processedEvents.Add(eventKey);
            return Task.FromResult(new PaymentCallbackResult(true, transitioned));
        }
    }

    private PaymentOrder GetRequired(string orderId) => _orders.TryGetValue(orderId, out var order)
        ? order
        : throw new KeyNotFoundException("找不到对应的支付记录。");
}

