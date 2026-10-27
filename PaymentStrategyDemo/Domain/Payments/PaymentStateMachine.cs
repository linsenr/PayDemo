namespace PaymentStrategyDemo.Domain.Payments;

/// <summary>集中维护支付单生命周期规则，避免用例和存储实现各自判断状态转换。</summary>
public static class PaymentStateMachine
{
    /// <summary>判断状态是否为不可再推进的支付终态。</summary>
    public static bool IsTerminal(PaymentStatus status) =>
        status is PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Expired;

    /// <summary>判断状态转换是否合法；同状态转换允许作为幂等更新。</summary>
    public static bool CanTransition(PaymentStatus current, PaymentStatus next)
    {
        if (!Enum.IsDefined(current) || !Enum.IsDefined(next))
        {
            return false;
        }

        // 渠道查询或重复通知可能再次报告当前状态；允许重复应用而不改变状态。
        if (current == next)
        {
            return true;
        }

        return current switch
        {
            PaymentStatus.Created => next == PaymentStatus.Processing,
            PaymentStatus.Processing => next is PaymentStatus.Pending
                or PaymentStatus.Succeeded
                or PaymentStatus.Failed
                or PaymentStatus.Expired,
            PaymentStatus.Pending => next is PaymentStatus.Succeeded
                or PaymentStatus.Failed
                or PaymentStatus.Expired,
            PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Expired => false,
            _ => false
        };
    }

    /// <summary>执行合法状态转换；非法转换明确失败，避免终态被迟到消息覆盖。</summary>
    public static PaymentOrder Transition(
        PaymentOrder order,
        PaymentStatus next,
        DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!CanTransition(order.Status, next))
        {
            throw new InvalidOperationException(
                $"不允许支付状态从 {order.Status} 转换为 {next}。");
        }

        if (order.Status == next)
        {
            // 幂等转换直接返回原对象，调用方不会误以为状态发生了变化。
            return order;
        }

        return order with { Status = next, UpdatedAt = changedAt };
    }
}
