using Microsoft.Extensions.Logging;
using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ContextMenuStartupTask(
    IServiceProvider services,
    ILogger<ContextMenuStartupTask> logger) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<ContextMenuStartupTask> _logger = logger;

    public string Name => "Register shell context menu";

    public AppStartupStage Stage => AppStartupStage.Background;

    public int Order => 1000;

    public int ShutdownOrder => 410;

    public async Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
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

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
