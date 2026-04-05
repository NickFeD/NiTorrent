using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentPreferences
{
    // Paths
    string DefaultDownloadPath { get; }

    // Rates (bytes/sec). 0 = unlimited
    int MaximumDownloadRate { get; }
    int MaximumUploadRate { get; }
    int MaximumDiskReadRate { get; }
    int MaximumDiskWriteRate { get; }

    // Network
    bool AllowDht { get; }
    bool AllowPortForwarding { get; }
    bool AllowLocalPeerDiscovery { get; }

    // Advanced
    int MaximumConnections { get; }
    int MaximumOpenFiles { get; }

    // Resume/metadata
    bool AutoSaveLoadFastResume { get; }
    bool AutoSaveLoadMagnetLinkMetadata { get; }
    TorrentFastResumeMode FastResumeMode { get; }

    // Window close behavior
    bool MinimizeToTrayOnClose { get; }
}
