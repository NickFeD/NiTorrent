using Microsoft.Extensions.Hosting;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownService(IHostApplicationLifetime applicationLifetime) : IAppShutdownService
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

    public void RequestShutdown()
        => _applicationLifetime.StopApplication();
}
