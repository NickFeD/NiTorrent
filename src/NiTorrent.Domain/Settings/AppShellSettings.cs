namespace NiTorrent.Domain.Settings;

public sealed class AppShellSettings
{
    public AppCloseBehavior CloseBehavior { get; set; } = AppCloseBehavior.MinimizeToTray;
}
