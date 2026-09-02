using System.Windows;

namespace MouseFx.Hooks;

/// <summary>
/// 鼠标移动合并器：低层钩子以轮询频率（125~1000Hz+）推送事件，若每条都调度一次
/// UI 拍，会产生海量小对象分配（闭包/委托/DispatcherOperation），且 UI 忙时
/// 处理的全是过期位置。本类让钩子线程只写最新坐标，每个 UI 拍最多消费一次，
/// 处理的永远是最新位置。
/// 线程约定：Push 只在钩子线程调用，Drain 只在 UI 线程调用；
/// x64 上 Point（两个 8 字节字段）读写原子，_queued 用 volatile 防重排。
/// </summary>
public sealed class MoveCoalescer
{
    private readonly Action<Point> _onDrain;
    private Point _pending;          // 钩子线程写、UI 线程读
    private volatile bool _queued;   // true = 已有待处理的调度

    public MoveCoalescer(Action<Point> onDrain) => _onDrain = onDrain;

    /// <summary>记录最新坐标。返回 true = 需要安排一次 UI 调度；false = 已有排队，只更新坐标。</summary>
    public bool Push(Point point)
    {
        _pending = point;
        if (_queued) return false;
        _queued = true;
        return true;
    }

    /// <summary>UI 拍内消费：先清标志再取值，保证竞态窗口内 Push 进来的最新坐标不会滞留。</summary>
    public void Drain()
    {
        _queued = false;
        _onDrain(_pending);
    }
}
