using Microsoft.Windows.AppLifecycle;

namespace NiTorrent.App.Services.AppLifecycle;

public interface IActivationQueue
{
    void Enqueue(AppActivationArguments activationArgs);

    void StopAccepting();

    Task DrainAsync(CancellationToken cancellationToken);
}
