using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using NiTorrent.Application.Torrents.Enum;

namespace NiTorrent.Infrastructure.Torrents;

internal static class TorrentLifecycleStateMapper
{
    public static TorrentLifecycleState Map(this TorrentState torrentState)
    {
        return torrentState switch
        {
            TorrentState.Stopped => TorrentLifecycleState.Stopped,
            TorrentState.Paused => TorrentLifecycleState.Paused,
            TorrentState.Downloading => TorrentLifecycleState.Downloading,
            TorrentState.Seeding => TorrentLifecycleState.Seeding,
            TorrentState.Hashing => TorrentLifecycleState.Checking,
            TorrentState.FetchingHashes => TorrentLifecycleState.Checking,
            TorrentState.Metadata => TorrentLifecycleState.FetchingMetadata,
            TorrentState.Error => TorrentLifecycleState.Error,
            TorrentState.HashingPaused => TorrentLifecycleState.Paused,
            TorrentState.Starting => TorrentLifecycleState.Unknown,
            TorrentState.Stopping => TorrentLifecycleState.Unknown,
            _ => TorrentLifecycleState.Unknown

        };
    }
}
