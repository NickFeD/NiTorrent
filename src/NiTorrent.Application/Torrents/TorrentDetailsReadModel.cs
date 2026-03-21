using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentDetailsReadModel
{
    public TorrentDetailsReadModel(TorrentSnapshot snapshot, TorrentEntrySettings settings)
    {
        Snapshot = snapshot;
        Settings = settings;
    }

    public TorrentSnapshot Snapshot { get; }
    public TorrentEntrySettings Settings { get; }
}
