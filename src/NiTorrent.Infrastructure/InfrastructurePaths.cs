namespace NiTorrent.Infrastructure;

public static class InfrastructurePaths
{
    public static readonly string RootDirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NiTorrent");

    public static readonly string AppConfigPath =
        Path.Combine(RootDirectoryPath, "AppConfig.json");

    public static readonly string TorrentConfigPath =
        Path.Combine(RootDirectoryPath, "TorrentConfig.json");

    public static readonly string TorrentEntrySettingsConfigPath =
        Path.Combine(RootDirectoryPath, "TorrentEntrySettings.json");
}
