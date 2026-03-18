using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Settings;

public sealed class JsonTorrentPreferences : ITorrentPreferences
{
    private readonly TorrentConfig _cfg;
    public JsonTorrentPreferences(TorrentConfig cfg) => _cfg = cfg;

    public string DefaultDownloadPath
    {
        get => _cfg.DefaultDownloadPath;
        set { _cfg.DefaultDownloadPath = value; _cfg.Save(); }
    }

    public int MaximumDownloadRate
    {
        get => _cfg.MaximumDownloadRate;
        set { _cfg.MaximumDownloadRate = value; _cfg.Save(); }
    }

    public int MaximumUploadRate
    {
        get => _cfg.MaximumUploadRate;
        set { _cfg.MaximumUploadRate = value; _cfg.Save(); }
    }

    public int MaximumDiskReadRate
    {
        get => _cfg.MaximumDiskReadRate;
        set { _cfg.MaximumDiskReadRate = value; _cfg.Save(); }
    }

    public int MaximumDiskWriteRate
    {
        get => _cfg.MaximumDiskWriteRate;
        set { _cfg.MaximumDiskWriteRate = value; _cfg.Save(); }
    }

    public bool AllowDht
    {
        get => _cfg.AllowDht;
        set { _cfg.AllowDht = value; _cfg.Save(); }
    }

    public bool AllowPortForwarding
    {
        get => _cfg.AllowPortForwarding;
        set { _cfg.AllowPortForwarding = value; _cfg.Save(); }
    }

    public bool AllowLocalPeerDiscovery
    {
        get => _cfg.AllowLocalPeerDiscovery;
        set { _cfg.AllowLocalPeerDiscovery = value; _cfg.Save(); }
    }

    public int MaximumConnections
    {
        get => _cfg.MaximumConnections;
        set { _cfg.MaximumConnections = value; _cfg.Save(); }
    }

    public int MaximumOpenFiles
    {
        get => _cfg.MaximumOpenFiles;
        set { _cfg.MaximumOpenFiles = value; _cfg.Save(); }
    }

    public bool AutoSaveLoadFastResume
    {
        get => _cfg.AutoSaveLoadFastResume;
        set { _cfg.AutoSaveLoadFastResume = value; _cfg.Save(); }
    }

    public bool AutoSaveLoadMagnetLinkMetadata
    {
        get => _cfg.AutoSaveLoadMagnetLinkMetadata;
        set { _cfg.AutoSaveLoadMagnetLinkMetadata = value; _cfg.Save(); }
    }

    public TorrentFastResumeMode FastResumeMode
    {
        get => _cfg.FastResumeMode;
        set { _cfg.FastResumeMode = value; _cfg.Save(); }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _cfg.MinimizeToTrayOnClose;
        set { _cfg.MinimizeToTrayOnClose = value; _cfg.Save(); }
    }

    public bool ShowCloseActionDialogOnClose
    {
        get => _cfg.ShowCloseActionDialogOnClose;
        set { _cfg.ShowCloseActionDialogOnClose = value; _cfg.Save(); }
    }
}
