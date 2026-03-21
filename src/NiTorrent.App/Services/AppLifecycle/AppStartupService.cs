using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Torrents;
using WinUIEx;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppStartupService : IAppStartupService
{
    private readonly ContextMenuService _menuService;
    private readonly ITorrentEngineStatusService _engineStatusService;
    private readonly ILogger<AppStartupService> _logger;

    public AppStartupService(
        ContextMenuService menuService,
        ITorrentEngineStatusService engineStatusService,
        ILogger<AppStartupService> logger)
    {
        _menuService = menuService;
        _engineStatusService = engineStatusService;
        _logger = logger;
    }

    public async Task StartHostAndShellAsync(IHost host)
    {
        try
        {
            await host.StartAsync().ConfigureAwait(false);

            if (!RuntimeHelper.IsPackaged())
                return;

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

            await _menuService.SaveAsync(menu).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application background initialization failed");
        }
    }

    public async Task InitializeTorrentEngineAsync()
    {
        try
        {
            await _engineStatusService.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Torrent engine initialization failed");
        }
    }
}
