using System.Threading;

namespace MouseFx.Settings;

/// <summary>
/// 落盘防抖：滑块拖动等高频变更在窗口内合并为一次保存，停手 Debounce 后才真正写盘
/// （否则每次像素级变动都是一次同步"全量序列化 + WriteAllText"，一次拖动几十上百次）。
/// 纯计时逻辑：TimeProvider 可注入，测试用手动时钟确定性推进；TimeProvider.System 的
/// 回调在线程池触发，UI 侧回调里需自行调度回 UI 线程。
/// </summary>
public sealed class DebouncedSaver : IDisposable
{
    /// <summary>默认防抖窗口：变更停手后 400ms 落盘。</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(400);

    private readonly Action _save;
    private readonly TimeSpan _delay;
    private readonly TimeProvider _timeProvider;
    private ITimer? _timer;
    private bool _disposed;

    public DebouncedSaver(Action save, TimeSpan? delay = null, TimeProvider? timeProvider = null)
    {
        _save = save;
        _delay = delay ?? DefaultDelay;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>有变更：重置防抖窗口。窗口内连续变更合并为一次保存。</summary>
    public void Schedule()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer?.Dispose();
        _timer = _timeProvider.CreateTimer(OnTimer, null, _delay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>立即保存并取消挂起的防抖（如设置窗口关闭时兜底）。</summary>
    public void FlushNow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer?.Dispose();
        _timer = null;
        _save();
    }

    private void OnTimer(object? state)
    {
        _timer = null;
        _save();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
