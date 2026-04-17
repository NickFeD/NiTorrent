using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;
using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentMetadataProvider(TorrentEngineCoordinator coordinator) : ITorrentMetadataProvider
{
    private readonly ClientEngine _clientEngine = coordinator.Engine;

    public async Task<TorrentMetadata> ExtractAsync(TorrentSource source, CancellationToken ct)
    {
        var torrentMetadata = await (source switch
        {
            TorrentSource.TorrentFile tf => MetadataFromFileAsync(tf.Path, ct),
            TorrentSource.Magnet m => MetadataFromMagnetAsync(m.Uri, ct),
            TorrentSource.TorrentBytes b => MetadataFromBytesAsync(b.Bytes, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        });
        return torrentMetadata;
    }

    private async Task<TorrentMetadata> MetadataFromBytesAsync(byte[] bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var torrent = await Torrent.LoadAsync(bytes);
        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new TorrentMetadata(
            torrent.InfoHashes.V1OrV2.ToHex(),
            torrent.Name,
            torrent.Size,
            files)
        {
            Source = new TorrentBytes(bytes)
        };
    }
    private async Task<TorrentMetadata> MetadataFromMagnetAsync(string magnetUri, CancellationToken ct)
    {
        var magnet = MagnetLink.Parse(magnetUri);
        var metadata = await _clientEngine.DownloadMetadataAsync(magnet, ct);
        return await MetadataFromBytesAsync(metadata.ToArray(), ct);
    }

    private async Task<TorrentMetadata> MetadataFromFileAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return await MetadataFromBytesAsync(bytes, ct);
    }
}
