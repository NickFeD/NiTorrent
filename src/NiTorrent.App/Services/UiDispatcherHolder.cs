
using Microsoft.UI.Dispatching;

namespace NiTorrent.App.Services;

public sealed class UiDispatcherHolder
{
    public DispatcherQueue? Queue { get; private set; }

    public void Initialize(DispatcherQueue queue) => Queue = queue;
}
