using MouseFx.Settings;

namespace MouseFx.Tests;

public class DebouncedSaverTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(400);

    [Fact]
    public void 窗口内多次变更只保存一次()
    {
        int saved = 0;
        var clock = new ManualTimeProvider();
        using var saver = new DebouncedSaver(() => saved++, Delay, clock);

        saver.Schedule();
        saver.Schedule();
        saver.Schedule();
        clock.Advance(Delay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, saved);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, saved);
    }

    [Fact]
    public void 重新变更会重置计时窗口()
    {
        int saved = 0;
        var clock = new ManualTimeProvider();
        using var saver = new DebouncedSaver(() => saved++, Delay, clock);

        saver.Schedule();
        clock.Advance(Delay * 0.9);
        saver.Schedule(); // 窗口重置
        clock.Advance(Delay * 0.9);
        Assert.Equal(0, saved);

        clock.Advance(Delay * 0.1);
        Assert.Equal(1, saved);
    }

    [Fact]
    public void FlushNow立即保存并取消挂起的防抖()
    {
        int saved = 0;
        var clock = new ManualTimeProvider();
        using var saver = new DebouncedSaver(() => saved++, Delay, clock);

        saver.Schedule();
        saver.FlushNow();
        Assert.Equal(1, saved);

        clock.Advance(Delay * 2);
        Assert.Equal(1, saved); // 挂起的防抖已取消，不重复保存
    }

    [Fact]
    public void 未有变更时FlushNow直接保存一次()
    {
        int saved = 0;
        using var saver = new DebouncedSaver(() => saved++, Delay, new ManualTimeProvider());

        saver.FlushNow();

        Assert.Equal(1, saved);
    }

    [Fact]
    public void Dispose后不再触发保存()
    {
        int saved = 0;
        var clock = new ManualTimeProvider();
        var saver = new DebouncedSaver(() => saved++, Delay, clock);

        saver.Schedule();
        saver.Dispose();
        clock.Advance(Delay * 2);

        Assert.Equal(0, saved);
        Assert.Throws<ObjectDisposedException>(saver.Schedule);
    }
}
