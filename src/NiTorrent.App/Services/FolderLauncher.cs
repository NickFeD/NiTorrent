using System.Diagnostics;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class FolderLauncher : IFolderLauncher
{
    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
