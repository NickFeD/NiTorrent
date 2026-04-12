using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.DTo;

public sealed record StartedTorrentDownloadResponse(
            Guid Id,
            string Name,
            string InfoHash);

public sealed record StartTorrentResponse(Guid TorrentId, string Status);

public sealed record PauseTorrentResponse(Guid TorrentId, string Status);
public sealed record DeleteTorrentResponse(Guid TorrentId);
