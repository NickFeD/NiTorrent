using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Settings;

public sealed class JsonTorrentPreferences : ITorrentPreferences
{
    private readonly TorrentConfig _cfg;
    public JsonTorrentPreferences(TorrentConfig cfg) => _cfg = cfg;

    public string DefaultDownloadPath => _cfg.DefaultDownloadPath;

    public int MaximumDownloadRate => _cfg.MaximumDownloadRate;

    public int MaximumUploadRate => _cfg.MaximumUploadRate;

    public int MaximumDiskReadRate => _cfg.MaximumDiskReadRate;

    public int MaximumDiskWriteRate => _cfg.MaximumDiskWriteRate;

    public bool AllowDht => _cfg.AllowDht;

    public bool AllowPortForwarding => _cfg.AllowPortForwarding;

    public bool AllowLocalPeerDiscovery => _cfg.AllowLocalPeerDiscovery;

    public int MaximumConnections => _cfg.MaximumConnections;

    public int MaximumOpenFiles => _cfg.MaximumOpenFiles;

    public bool AutoSaveLoadFastResume => _cfg.AutoSaveLoadFastResume;

    public bool AutoSaveLoadMagnetLinkMetadata => _cfg.AutoSaveLoadMagnetLinkMetadata;

    public TorrentFastResumeMode FastResumeMode => _cfg.FastResumeMode;

    public bool MinimizeToTrayOnClose => _cfg.MinimizeToTrayOnClose;
}
