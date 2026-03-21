namespace NiTorrent.Domain.Settings;

public static class AppShellClosePolicy
{
    public static AppShellCloseAction Resolve(AppCloseBehavior behavior, AppShellCloseRequestSource source)
    {
        if (source == AppShellCloseRequestSource.TrayExit)
            return AppShellCloseAction.ExitApplication;

        return behavior switch
        {
            AppCloseBehavior.MinimizeToTray => AppShellCloseAction.MinimizeToTray,
            AppCloseBehavior.ExitApplication => AppShellCloseAction.ExitApplication,
            AppCloseBehavior.AskUser => AppShellCloseAction.AskUser,
            _ => AppShellCloseAction.ExitApplication,
        };
    }
}
