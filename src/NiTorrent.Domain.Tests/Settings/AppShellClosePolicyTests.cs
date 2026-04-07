using NiTorrent.Domain.Settings;
using Xunit;

namespace NiTorrent.Domain.Tests.Settings;

public sealed class AppShellClosePolicyTests
{
    [Theory]
    [InlineData(AppCloseBehavior.ExitApplication)]
    [InlineData(AppCloseBehavior.MinimizeToTray)]
    [InlineData(AppCloseBehavior.AskUser)]
    public void Resolve_TrayExit_AlwaysForcesExit(AppCloseBehavior configuredBehavior)
    {
        var action = AppShellClosePolicy.Resolve(configuredBehavior, AppShellCloseRequestSource.TrayExit);

        Assert.Equal(AppShellCloseAction.ExitApplication, action);
    }
}
