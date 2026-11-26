namespace NiTorrent.Presentation;

/// <summary>Desktop visibility only. Suspending pages does not affect the tray or torrent session.</summary>
public sealed class DesktopObservationLifetime
{
    public bool IsVisible { get; private set; } = true;
    public event Action<bool>? VisibilityChanged;
    public void SetVisible(bool visible)
    {
        if (IsVisible == visible) return;
        IsVisible = visible;
        VisibilityChanged?.Invoke(visible);
    }
}
