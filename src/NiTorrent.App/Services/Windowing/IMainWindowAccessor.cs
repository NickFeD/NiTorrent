using Microsoft.UI.Xaml;

namespace NiTorrent.App.Services.Windowing;

public interface IMainWindowAccessor
{
    Window? Current { get; }
    void Set(Window window);
}
