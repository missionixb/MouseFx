using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

public interface IEffect
{
    string Name { get; }
    bool Enabled { get; set; }
    void OnMouseDown(Point position);
    void OnMouseMove(Point position);
    void Update(TimeSpan delta);
    void Draw(DrawingContext dc);
}
