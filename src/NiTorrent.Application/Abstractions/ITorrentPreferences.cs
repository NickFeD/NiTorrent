using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentPreferences
{
    // Paths
    string DefaultDownloadPath { get; set; }

    // Rates (bytes/sec). 0 = unlimited
    int MaximumDownloadRate { get; set; }
    int MaximumUploadRate { get; set; }
    int MaximumDiskReadRate { get; set; }
    int MaximumDiskWriteRate { get; set; }

    // Network
    bool AllowDht { get; set; }
    bool AllowPortForwarding { get; set; }
    bool AllowLocalPeerDiscovery { get; set; }

    // Advanced
    int MaximumConnections { get; set; }
    int MaximumOpenFiles { get; set; }

    // Resume/metadata
    bool AutoSaveLoadFastResume { get; set; }
    bool AutoSaveLoadMagnetLinkMetadata { get; set; }
    TorrentFastResumeMode FastResumeMode { get; set; }

    // Window close behavior
    bool MinimizeToTrayOnClose { get; set; }
}
