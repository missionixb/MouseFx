using System.Windows;
using System.Windows.Media;
using MouseFx.Settings;

namespace MouseFx.Effects;

/// <summary>
/// 点击波纹的形状几何。均为单位尺寸（宽约 2×半径，坐标以 (0,0) 为中心），
/// 绘制时按波纹半径缩放并平移到点击位置。全部缓存 + Freeze。
/// </summary>
public static class RippleShapes
{
    private static readonly Geometry Circle = Freeze(new EllipseGeometry(new Point(0, 0), 1, 1));
    private static readonly Geometry Heart = CreateHeart();
    private static readonly Geometry Star = CreateStar();

    public static Geometry For(RippleShape shape) => shape switch
    {
        RippleShape.Circle => Circle,
        RippleShape.Heart => Heart,
        RippleShape.Star => Star,
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "未知的波纹形状"),
    };

    private static Geometry Freeze(Geometry g)
    {
        g.Freeze();
        return g;
    }

    /// <summary>
    /// 爱心：圆润简洁版（比例取自 Material Design 经典心形图标，归一化到宽 2.0、高 2.0）。
    /// 两个标准圆瓣 + 圆弧侧身平滑收至底尖，瓣间为浅圆凹。
    /// </summary>
    private static Geometry CreateHeart()
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, 1.0), true, true);                                        // 底部尖点
            ctx.BezierTo(new Point(-0.66, 0.35), new Point(-1.0, 0.01), new Point(-1.0, -0.40), true, false);  // 左侧弧→最宽处
            ctx.BezierTo(new Point(-1.0, -0.73), new Point(-0.76, -1.0), new Point(-0.45, -1.0), true, false); // 左瓣圆顶
            ctx.BezierTo(new Point(-0.28, -1.0), new Point(-0.11, -0.91), new Point(0, -0.77), true, false);   // 左瓣→中央浅凹
            ctx.BezierTo(new Point(0.11, -0.91), new Point(0.28, -1.0), new Point(0.45, -1.0), true, false);   // 浅凹→右瓣
            ctx.BezierTo(new Point(0.76, -1.0), new Point(1.0, -0.73), new Point(1.0, -0.40), true, false);    // 右瓣圆顶
            ctx.BezierTo(new Point(1.0, 0.01), new Point(0.66, 0.35), new Point(0, 1.0), true, false);         // 右侧弧→回底尖
        }
        return Freeze(g);
    }

    /// <summary>
    /// 五角星：简约经典版。外接圆半径 1.0、内半径 0.382（正五角星黄金比例），顶点朝上；
    /// 所有顶点整体相对原点下移 0.1，使包围盒竖直居中。
    /// </summary>
    private static Geometry CreateStar()
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, -0.9), true, true);            // 顶点
            ctx.LineTo(new Point(0.225, -0.209), true, false);          // 右上内凹
            ctx.LineTo(new Point(0.951, -0.209), true, false);          // 右外角
            ctx.LineTo(new Point(0.363, 0.218), true, false);           // 右下内凹
            ctx.LineTo(new Point(0.588, 0.909), true, false);           // 右下外角
            ctx.LineTo(new Point(0, 0.482), true, false);               // 底部内凹
            ctx.LineTo(new Point(-0.588, 0.909), true, false);          // 左下外角
            ctx.LineTo(new Point(-0.363, 0.218), true, false);          // 左下内凹
            ctx.LineTo(new Point(-0.951, -0.209), true, false);         // 左外角
            ctx.LineTo(new Point(-0.225, -0.209), true, false);         // 左上内凹
        }
        return Freeze(g);
    }
}
