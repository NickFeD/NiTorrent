using NiTorrent.Application.Settings.Enums;

namespace NiTorrent.Application.Settings;

public class AppSettings
{
    public TorrentEngineSettings EngineSettings { get; set; } = new TorrentEngineSettings();
    public AppCloseBehavior CloseBehavior { get; set; }
}
