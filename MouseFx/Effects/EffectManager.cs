using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

public sealed class EffectManager
{
    private readonly List<IEffect> _effects = new();

    public IReadOnlyList<IEffect> Effects => _effects;

    public void Register(IEffect effect) => _effects.Add(effect);

    public void HandleMouseDown(Point position)
    {
        foreach (var effect in _effects)
            if (effect.Enabled) effect.OnMouseDown(position);
    }

    public void HandleMouseMove(Point position)
    {
        foreach (var effect in _effects)
            if (effect.Enabled) effect.OnMouseMove(position);
    }

    public void UpdateAll(TimeSpan delta)
    {
        foreach (var effect in _effects)
            if (effect.Enabled) effect.Update(delta);
    }

    public void DrawAll(DrawingContext dc)
    {
        foreach (var effect in _effects)
            if (effect.Enabled) effect.Draw(dc);
    }
}
