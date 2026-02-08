using NiTorrent.Application.Abstractions;
using Windows.System;

namespace NiTorrent.App.Services;

public sealed class WinUriLauncher : IUriLauncher
{
    public Task LaunchAsync(Uri uri, CancellationToken ct = default)
        => Launcher.LaunchUriAsync(uri).AsTask(ct);
}

