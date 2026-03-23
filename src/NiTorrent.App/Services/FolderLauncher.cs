using System.Diagnostics;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;

namespace NiTorrent.App.Services;

public sealed class FolderLauncher : IFolderLauncher
{
    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new UserVisibleException("Папка для этого торрента недоступна.");

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
