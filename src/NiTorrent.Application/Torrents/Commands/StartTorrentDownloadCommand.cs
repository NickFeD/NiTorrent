using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Commands;

public class StartTorrentDownloadCommand
{
    public TorrentSource Source { get; internal set; }
    public string DownloadDirectory { get; internal set; }
    public List<TorrentFileEntry> Files { get; internal set; }
}
