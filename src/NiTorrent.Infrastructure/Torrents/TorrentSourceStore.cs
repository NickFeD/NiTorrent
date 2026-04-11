using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentSourceStore : ITorrentSourceStore
{
    private readonly string _sourcesRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TorrentSourceStore(IAppStorageService storage)
    {
        _sourcesRoot = storage.GetLocalPath(@"Torrents\sources");
        storage.EnsureDirectory(_sourcesRoot);
    }

    public async Task SaveAsync(TorrentId id, TorrentKey key, byte[] torrentBytes, CancellationToken ct = default)
    {
        if (torrentBytes.Length == 0)
            throw new ArgumentException("Torrent source bytes are empty.", nameof(torrentBytes));

        var path = GetPath(id);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.WriteAllBytesAsync(path, torrentBytes, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]?> TryLoadAsync(TorrentId id, TorrentKey key, CancellationToken ct = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
            return null;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
                return null;

            return await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(TorrentId id, CancellationToken ct = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
            return;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetPath(TorrentId id)
        => Path.Combine(_sourcesRoot, $"{id.Value:N}.torrent");
}
