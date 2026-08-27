using System.Windows;
using System.Windows.Media;
using MouseFx.Settings;

namespace MouseFx.Effects;

/// <summary>
/// 点击波纹的形状几何。均为单位尺寸（宽约 2×半径、高约 2×半径，坐标以 (0,0) 为中心），
/// 绘制时按波纹半径缩放并平移到点击位置。全部缓存 + Freeze。
/// </summary>
public static class RippleShapes
{
    private static readonly Geometry Circle = Freeze(new EllipseGeometry(new Point(0, 0), 1, 1));
    private static readonly Geometry Heart = CreateHeart();
    private static readonly Geometry Clover = CreateClover();
    private static readonly Geometry Note = CreateNote();

    public static Geometry For(RippleShape shape) => shape switch
    {
        RippleShape.Heart => Heart,
        RippleShape.Clover => Clover,
        RippleShape.Note => Note,
        _ => Circle,
    };

    private static Geometry Freeze(Geometry g)
    {
        g.Freeze();
        return g;
    }

    /// <summary>爱心：底部尖点朝下，两侧双弧线（宽约 2.1，高约 1.8）。</summary>
    private static Geometry CreateHeart()
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, 0.95), true, true);                              // 底部尖点
            ctx.BezierTo(new Point(-0.5, 0.45), new Point(-1.05, 0.25), new Point(-1.05, -0.15), true, false); // 左下弧
            ctx.BezierTo(new Point(-1.05, -0.65), new Point(-0.55, -0.85), new Point(0, -0.4), true, false);   // 左顶到中间凹
            ctx.BezierTo(new Point(0.55, -0.85), new Point(1.05, -0.65), new Point(1.05, -0.15), true, false); // 右顶
            ctx.BezierTo(new Point(1.05, 0.25), new Point(0.5, 0.45), new Point(0, 0.95), true, false);        // 右下回尖
        }
        return Freeze(g);
    }

    /// <summary>四叶草：四个圆瓣 + 中心小圆连接（宽高约 2.08）。</summary>
    private static Geometry CreateClover()
    {
        const double leaf = 0.52;
        var group = new GeometryGroup();
        group.Children.Add(new EllipseGeometry(new Point(0, -leaf), leaf, leaf));
        group.Children.Add(new EllipseGeometry(new Point(0, leaf), leaf, leaf));
        group.Children.Add(new EllipseGeometry(new Point(-leaf, 0), leaf, leaf));
        group.Children.Add(new EllipseGeometry(new Point(leaf, 0), leaf, leaf));
        group.Children.Add(new EllipseGeometry(new Point(0, 0), 0.2, 0.2));
        return Freeze(group);
    }

    /// <summary>八分音符：倾斜椭圆头 + 杆 + 向左弯曲的旗（高约 1.65，宽约 1.2）。</summary>
    private static Geometry CreateNote()
    {
        var group = new GeometryGroup();

        // 杆
        group.Children.Add(new RectangleGeometry(new Rect(-0.07, -1.15, 0.14, 1.5)));

        // 音符头（倾斜椭圆）
        var head = new EllipseGeometry(new Point(0.12, 0.5), 0.42, 0.34);
        head.Transform = new RotateTransform(-20, 0.12, 0.5);
        group.Children.Add(head);

        // 旗（杆顶向左弯曲的曲线区域）
        var flag = new StreamGeometry();
        using (var ctx = flag.Open())
        {
            ctx.BeginFigure(new Point(-0.07, -1.15), true, true);
            ctx.BezierTo(new Point(-0.55, -1.05), new Point(-0.6, -0.6), new Point(-0.15, -0.3), true, false);
            ctx.BezierTo(new Point(-0.35, -0.6), new Point(-0.3, -0.9), new Point(-0.07, -0.95), true, false);
        }
        group.Children.Add(flag);

        return Freeze(group);
    }
}
