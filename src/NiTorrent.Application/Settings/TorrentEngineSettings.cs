
using NiTorrent.Application.Settings.Enums;

namespace NiTorrent.Application.Settings;

public class TorrentEngineSettings
{
    public int MaximumDownloadRate { get; set; } = 0; // 0 unlimited
    public int MaximumUploadRate { get; set; } = 0;

    public int MaximumDiskReadRate { get; set; } = 0;
    public int MaximumDiskWriteRate { get; set; } = 0;

    public bool AllowDht { get; set; } = true;
    public bool AllowPortForwarding { get; set; } = true;
    public bool AllowLocalPeerDiscovery { get; set; } = true;

    public int MaximumConnections { get; set; } = 200;
    public int MaximumOpenFiles { get; set; } = 100;

    public bool AutoSaveLoadFastResume { get; set; } = true;
    public bool AutoSaveLoadMagnetLinkMetadata { get; set; } = true;
    public TorrentFastResumeMode FastResumeMode { get; set; } = TorrentFastResumeMode.BestEffort;
}
