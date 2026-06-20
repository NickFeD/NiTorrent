using Microsoft.Windows.AppLifecycle;

namespace NiTorrent.App.Services.AppLifecycle;

public interface INiTorrentApplication
{
    Task StartAsync(AppActivationArguments activationArgs, CancellationToken cancellationToken);

    Task HandleActivationAsync(AppActivationArguments activationArgs, CancellationToken cancellationToken);

    Task ShutdownAsync(AppShutdownReason reason, CancellationToken cancellationToken);
}
