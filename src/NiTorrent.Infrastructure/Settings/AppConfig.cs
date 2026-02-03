using Nucs.JsonSettings.Modulation;
using Nucs.JsonSettings.Examples;


namespace NiTorrent.Infrastructure.Settings;

public partial class AppConfig : NotifiyingJsonSettings, IVersionable
{
    private string lastUpdateCheck;

    [EnforcedVersion("1.0.0.0")]
    public Version Version { get; set; } = new(1, 0, 0, 0);

    private string fileName { get; set; } = InfrastructurePaths.AppConfigPath;

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public override string FileName
    {
        get => fileName;
        set
        {
            if (Equals(value, fileName)) return;
            fileName = value;
            OnPropertyChanged(nameof(fileName));
            Save();
        }
    }

    public string LastUpdateCheck
    {
        get => lastUpdateCheck;
        set
        {
            if (Equals(value, lastUpdateCheck)) return;
            lastUpdateCheck = value;
            OnPropertyChanged(nameof(lastUpdateCheck));
            Save();
        }
    }
}
