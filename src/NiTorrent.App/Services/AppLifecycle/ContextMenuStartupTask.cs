using Microsoft.Extensions.Logging;
using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ContextMenuStartupTask(
    IServiceProvider services,
    ILogger<ContextMenuStartupTask> logger) : IAppStartupTask
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<ContextMenuStartupTask> _logger = logger;

    public StartupStage Stage => StartupStage.Background;

    public int Order => 1000;

    public bool CanRunInParallel => true;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (!RuntimeHelper.IsPackaged())
            return;

        var menuService = _services.GetRequiredService<ContextMenuService>();

        var menu = new ContextMenuItem
        {
            Title = "Open NiTorrent.App Here",
            Param = @"""{path}""",
            AcceptFileFlag = (int)FileMatchFlagEnum.All,
            AcceptDirectoryFlag = (int)(DirectoryMatchFlagEnum.Directory | DirectoryMatchFlagEnum.Background | DirectoryMatchFlagEnum.Desktop),
            AcceptMultipleFilesFlag = (int)FilesMatchFlagEnum.Each,
            Index = 0,
            Enabled = true,
            Icon = ProcessInfoHelper.GetFileVersionInfo().FileName,
            Exe = "NiTorrent.App.exe"
        };

        await menuService.SaveAsync(menu);
        _logger.LogInformation("Context menu registration updated");
    }
}
