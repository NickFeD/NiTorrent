using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentMetadataProvider
{
    Task<TorrentMetadata> ExtractAsync(TorrentSource source, CancellationToken ct);
}
