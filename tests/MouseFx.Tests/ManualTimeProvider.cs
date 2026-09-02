namespace MouseFx.Tests;

/// <summary>
/// 手动推进时钟的 TimeProvider + 可控 ITimer，用于确定性测试防抖逻辑。
/// 只实现 DebouncedSaver 用到的单发（period=Infinite）语义。
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly List<ManualTimer> _timers = new();

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>把时钟拨快指定时长，触发到期的定时器。</summary>
    public void Advance(TimeSpan time)
    {
        _now += time;
        foreach (var timer in _timers.ToArray())
            timer.FireIfDue(_now);
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _clock;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private DateTimeOffset _dueAt = DateTimeOffset.MaxValue;

        public ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state)
        {
            _clock = clock;
            _callback = callback;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _dueAt = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : _clock.GetUtcNow() + dueTime;
            return true;
        }

        public void FireIfDue(DateTimeOffset now)
        {
            if (now < _dueAt) return;
            _dueAt = DateTimeOffset.MaxValue; // 单发：触发后失效
            _callback(_state);
        }

        public void Dispose() => _dueAt = DateTimeOffset.MaxValue;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
