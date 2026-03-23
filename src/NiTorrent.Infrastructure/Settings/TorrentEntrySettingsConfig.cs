using Nucs.JsonSettings.Examples;
using Nucs.JsonSettings.Modulation;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Settings;

public sealed partial class TorrentEntrySettingsConfig : NotifiyingJsonSettings, IVersionable
{
    [EnforcedVersion("1.0.0.0")]
    public Version Version { get; set; } = new(1, 0, 0, 0);

    private string fileName = InfrastructurePaths.TorrentEntrySettingsConfigPath;
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

    public Dictionary<string, TorrentEntrySettings> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
