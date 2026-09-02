using System.Windows;
using MouseFx.Hooks;

namespace MouseFx.Tests;

public class MoveCoalescerTests
{
    [Fact]
    public void 排队中的多次推送合并_Drain只消费最新一次()
    {
        var delivered = new List<Point>();
        var pump = new MoveCoalescer(delivered.Add);

        Assert.True(pump.Push(new Point(1, 1)));   // 首次：需要安排调度
        Assert.False(pump.Push(new Point(2, 2)));  // 已排队：只更新坐标
        Assert.False(pump.Push(new Point(3, 3)));
        pump.Drain();

        var point = Assert.Single(delivered);
        Assert.Equal(new Point(3, 3), point);      // 消费的是最新位置
    }

    [Fact]
    public void Drain后可以再次安排调度()
    {
        var delivered = new List<Point>();
        var pump = new MoveCoalescer(delivered.Add);

        pump.Push(new Point(1, 1));
        pump.Drain();

        Assert.True(pump.Push(new Point(2, 2)));   // 新一轮推送重新要求安排
        pump.Drain();
        Assert.Equal(2, delivered.Count);
        Assert.Equal(new Point(2, 2), delivered[^1]);
    }

    [Fact]
    public async Task 并发推送与消费_消费序单调且最终为最新坐标()
    {
        var delivered = new List<Point>();
        var pump = new MoveCoalescer(delivered.Add);
        int pending = 0;
        bool writerDone = false;

        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 10_000; i++)
                if (pump.Push(new Point(i, 0)))
                    Interlocked.Increment(ref pending);
            Volatile.Write(ref writerDone, true);
        });

        // 模拟 UI 拍：有排队就消费一次（真实接线 = Dispatcher.BeginInvoke）
        while (!Volatile.Read(ref writerDone) || Volatile.Read(ref pending) > delivered.Count)
        {
            if (Volatile.Read(ref pending) > delivered.Count)
                pump.Drain();
            else
                Thread.Yield();
        }
        await writer;

        Assert.True(delivered.Count >= 1);
        for (int i = 1; i < delivered.Count; i++)  // 单写者坐标递增，消费序不得回退
            Assert.True(delivered[i].X >= delivered[i - 1].X);
        Assert.Equal(9_999, delivered[^1].X);      // 最后写入的坐标最终一定被消费
    }
}
