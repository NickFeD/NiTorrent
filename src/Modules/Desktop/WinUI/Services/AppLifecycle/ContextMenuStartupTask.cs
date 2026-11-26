using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ContextMenuStartupTask(
    IServiceProvider services,
    ILogger<ContextMenuStartupTask> logger) : IHostedService
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<ContextMenuStartupTask> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
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

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
