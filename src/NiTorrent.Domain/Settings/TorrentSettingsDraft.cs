namespace NiTorrent.Domain.Settings;

public sealed record TorrentSettingsDraft(
    string DefaultDownloadPath,
    int MaximumDownloadRate,
    int MaximumUploadRate,
    int MaximumDiskReadRate,
    int MaximumDiskWriteRate,
    bool AllowDht,
    bool AllowPortForwarding,
    bool AllowLocalPeerDiscovery,
    int MaximumConnections,
    int MaximumOpenFiles,
    bool AutoSaveLoadFastResume,
    bool AutoSaveLoadMagnetLinkMetadata,
    TorrentFastResumeMode FastResumeMode,
    AppCloseBehavior CloseBehavior
);
