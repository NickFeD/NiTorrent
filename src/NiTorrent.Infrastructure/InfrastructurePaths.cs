namespace NiTorrent.Infrastructure;

public static class InfrastructurePaths
{
    public static readonly string RootDirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NiTorrent");

    public static readonly string AppConfigPath =
        Path.Combine(RootDirectoryPath, "AppConfig.json");
}
