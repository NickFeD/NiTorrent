using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure;

internal class TorrentDownloadFactory : ITorrentDownloadFactory
{
    public TorrentDownload Create(TorrentMetadata metadata, List<TorrentFileEntry> torrentFiles, string DownloadDirectory)
    {
        return new TorrentDownload(Guid.NewGuid(), metadata.InfoHash, metadata.Name, DownloadDirectory)
        {
            FileEntries = torrentFiles,
        };
    }
}
