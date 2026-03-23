namespace NiTorrent.Domain.Settings;

public sealed record GlobalTorrentSettings(
    AppCloseBehavior CloseBehavior,
    string DownloadDirectory,
    int ListenPort,
    bool EnablePortForwarding,
    bool EnableDht,
    bool EnableLocalPeerDiscovery,
    bool EnableAutoStartOnRestore,
    int GlobalDownloadLimitKiB,
    int GlobalUploadLimitKiB
);
