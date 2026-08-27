namespace MouseFx.Platform;

public interface IMouseHookService : IDisposable
{
    event Action<Point>? MouseMove;
    event Action<Point>? MouseDown;
    bool IsRunning { get; }
    void Start();
    void Stop();
}
