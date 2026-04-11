using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentDownloadFactory
{
    TorrentDownload Create(TorrentMetadata metadata,List<TorrentFileEntry> torrentFiles, string DownloadDirectory);
}
