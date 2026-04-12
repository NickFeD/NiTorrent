using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Presentation.Abstractions;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppActivationService : IAppActivationService
{
    private readonly IDialogService _dialogService;
    private readonly ILogger<AppActivationService> _logger;

    public AppActivationService(
        IDialogService dialogService,
        ILogger<AppActivationService> logger)
    {
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task HandleAsync(AppActivationArguments args, Action showMainWindow, Action startBackgroundInitialization)
    {
        try
        {
            await HandleCoreAsync(args, showMainWindow, startBackgroundInitialization).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File activation handling failed");

            try
            {
                await _dialogService.ShowTextAsync("Не удалось открыть торрент-файл", UserErrorMapper.ToMessage(ex, "Не удалось открыть торрент-файл.")).ConfigureAwait(false);
            }
            catch (Exception dialogEx)
            {
                _logger.LogWarning(dialogEx, "Failed to show file activation error dialog");
            }
        }
    }

    private async Task HandleCoreAsync(AppActivationArguments args, Action showMainWindow, Action startBackgroundInitialization)
    {
        if (args.Kind != ExtendedActivationKind.File || args.Data is not FileActivatedEventArgs fileArgs)
            return;

        startBackgroundInitialization();
        showMainWindow();

        foreach (var item in fileArgs.Files)
        {
            if (item is not StorageFile file || !file.FileType.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                continue;
        }
    }
}
