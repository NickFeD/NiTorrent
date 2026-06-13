using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownService : IAppShutdownService
{
    private Func<Task>? _shutdownAsync;

    public void Initialize(Func<Task> shutdownAsync)
        => _shutdownAsync = shutdownAsync ?? throw new ArgumentNullException(nameof(shutdownAsync));

    public void RequestShutdown()
    {
        var shutdownAsync = _shutdownAsync
            ?? throw new InvalidOperationException("Application shutdown pipeline is not initialized.");

        _ = shutdownAsync();
    }
}
