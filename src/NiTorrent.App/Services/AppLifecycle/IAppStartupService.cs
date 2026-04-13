using Microsoft.Extensions.Hosting;

namespace NiTorrent.App.Services.AppLifecycle;

public interface IAppStartupService
{
    Task StartHostAndShellAsync(IHost host);
    Task InitializeTorrentEngineAsync(CancellationToken ct);
}
