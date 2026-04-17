using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Settings;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppStartupService(
    ContextMenuService menuService,
    IEngineSettingsService engineSettingsService,
    ILogger<AppStartupService> logger) : IAppStartupService
{
    private readonly ContextMenuService _menuService = menuService;
    private readonly ILogger<AppStartupService> _logger = logger;
    private readonly IEngineSettingsService _engineSettingsService = engineSettingsService;

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

            await _menuService.SaveAsync(menu);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application background initialization failed");
        }
    }

    public async Task InitializeTorrentEngineAsync(CancellationToken ct)
    {
        try
        {
            await _engineSettingsService.InitializeAsync(ct);
            //await _restoreWorkflow.ExecuteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Torrent engine initialization failed");
        }
    }
}
