using Microsoft.UI.Xaml;

namespace NiTorrent.App.Services.Windowing;

public sealed class MainWindowAccessor : IMainWindowAccessor
{
    public Window? Current { get; private set; }

    public void Set(Window window)
    {
        Current = window;
    }
}
