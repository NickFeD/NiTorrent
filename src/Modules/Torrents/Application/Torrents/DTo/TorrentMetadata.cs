using NiTorrent.Domain.Torrents;
using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Application.Torrents.DTo;

public class TorrentMetadata(string infoHash, string name, long size, List<TorrentFileEntry> files)
{
    public TorrentSource Source { get; set; } = new TorrentFile("");
    public string InfoHash { get; set; } = infoHash;
    public string Name { get; set; } = name;
    public long TotalSize { get; set; } = size;
    public List<TorrentFileEntry> Files { get; set; } = files;
}
