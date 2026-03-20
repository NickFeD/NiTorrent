using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentSourceResolver
{
    public async Task<Torrent> ResolveAsync(
        TorrentSource source,
        Func<CancellationToken, Task> ensureStartedAsync,
        Func<ClientEngine> getEngine,
        CancellationToken ct)
    {
        switch (source)
        {
            case TorrentSource.TorrentFile tf:
                return await Torrent.LoadAsync(tf.Path).ConfigureAwait(false);

            case TorrentSource.Magnet m:
                await ensureStartedAsync(ct).ConfigureAwait(false);
                var magnet = MagnetLink.Parse(m.Uri);
                var metadata = await getEngine().DownloadMetadataAsync(magnet, ct).ConfigureAwait(false);
                return await Torrent.LoadAsync(memory: metadata.ToArray()).ConfigureAwait(false);

            case TorrentSource.TorrentBytes b:
                return await Torrent.LoadAsync(b.Bytes).ConfigureAwait(false);

            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }
}
