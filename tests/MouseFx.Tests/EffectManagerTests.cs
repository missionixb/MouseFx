using System.Windows;
using System.Windows.Media;
using MouseFx.Effects;
using Xunit;

namespace MouseFx.Tests;

public sealed class FakeEffect : IEffect
{
    public string Name { get; init; } = "fake";
    public bool Enabled { get; set; }
    public bool HasVisual { get; init; }
    public int DownCalls { get; private set; }
    public int MoveCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int DrawCalls { get; private set; }

    public void OnMouseDown(Point position) => DownCalls++;
    public void OnMouseMove(Point position) => MoveCalls++;
    public void Update(TimeSpan delta) => UpdateCalls++;
    public void Draw(DrawingContext dc) => DrawCalls++;
}

public class EffectManagerTests
{
    [Fact]
    public void 事件只分发给启用的特效()
    {
        var manager = new EffectManager();
        var enabled = new FakeEffect { Enabled = true };
        var disabled = new FakeEffect { Enabled = false };
        manager.Register(enabled);
        manager.Register(disabled);

        manager.HandleMouseDown(new Point(10, 20));
        manager.HandleMouseMove(new Point(30, 40));
        manager.UpdateAll(TimeSpan.FromMilliseconds(16));

        Assert.Equal(1, enabled.DownCalls);
        Assert.Equal(1, enabled.MoveCalls);
        Assert.Equal(1, enabled.UpdateCalls);
        Assert.Equal(0, disabled.DownCalls);
        Assert.Equal(0, disabled.MoveCalls);
        Assert.Equal(0, disabled.UpdateCalls);
    }

    [Fact]
    public void 注册顺序即Effects顺序()
    {
        var manager = new EffectManager();
        var a = new FakeEffect { Name = "A" };
        var b = new FakeEffect { Name = "B" };
        manager.Register(a);
        manager.Register(b);

        Assert.Equal(2, manager.Effects.Count);
        Assert.Same(a, manager.Effects[0]);
        Assert.Same(b, manager.Effects[1]);
    }

    [Fact]
    public void HasVisual只看已启用的特效()
    {
        var manager = new EffectManager();
        var spark = new SparkEffect(new Random(1)) { Enabled = false };
        manager.Register(spark);
        spark.OnMouseMove(new System.Windows.Point(0, 0));

        Assert.False(manager.HasVisual); // 禁用时不计入

        spark.Enabled = true;
        manager.UpdateAll(TimeSpan.FromMilliseconds(16));
        Assert.True(manager.HasVisual); // 禁用取消且已产生画面
    }
}
