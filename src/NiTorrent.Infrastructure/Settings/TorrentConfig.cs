using Nucs.JsonSettings.Modulation;
using Nucs.JsonSettings.Examples;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Settings;

public sealed partial class TorrentConfig : NotifiyingJsonSettings, IVersionable
{
    [EnforcedVersion("1.0.0.0")]
    public Version Version { get; set; } = new(1, 0, 0, 0);

    private string fileName = InfrastructurePaths.TorrentConfigPath;
    public override string FileName
    {
        get => fileName;
        set
        {
            if (Equals(value, fileName)) return;
            fileName = value;
            OnPropertyChanged(nameof(FileName));
            Save();
        }
    }

    // Defaults
    public string DefaultDownloadPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

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

    public bool MinimizeToTrayOnClose { get; set; } = true;
}
