using Microsoft.Extensions.Hosting;
using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class HostStopShutdownTask() : IAppShutdownTask
{
    public int Order => 1000;

    public Task ExecuteAsync(CancellationToken ct)
        => App.Host.StopAsync(ct);
}
