using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class OpenTorrentFolderUseCase(IFolderLauncher folderLauncher)
{
    public Task ExecuteAsync(string path, CancellationToken ct = default)
        => folderLauncher.OpenAsync(path, ct);
}
