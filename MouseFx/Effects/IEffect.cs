using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

public interface IEffect
{
    string Name { get; }
    bool Enabled { get; set; }

    /// <summary>当前是否有可见画面（脏标记：OverlayWindow 据此跳过无内容的重绘帧）。</summary>
    bool HasVisual { get; }

    void OnMouseDown(Point position);
    void OnMouseMove(Point position);
    void Update(TimeSpan delta);
    void Draw(DrawingContext dc);
}
