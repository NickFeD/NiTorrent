using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using Nucs.JsonSettings;

namespace NiTorrent.Infrastructure.Settings;

public class AppJsonSettings : JsonSettings
{
    public override string FileName { get; set; } = null!;

    public AppJsonSettings()
        : base()
    {
    }
    public AppJsonSettings(string fileName) : base(fileName)
    {
    }

    public TorrentEngineSettings EngineSettings { get; set; } = new TorrentEngineSettings();
}
