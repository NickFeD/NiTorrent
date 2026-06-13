using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.Application.Common;
using NiTorrent.Presentation.Abstractions;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppActivationService : IAppActivationService
{
    private readonly IDialogService _dialogService;
    private readonly ILogger<AppActivationService> _logger;
    private readonly IAppShellLifecycle _shellLifecycle;

    public AppActivationService(
        IDialogService dialogService,
        ILogger<AppActivationService> logger,
        IAppShellLifecycle shellLifecycle)
    {
        _dialogService = dialogService;
        _logger = logger;
        _shellLifecycle = shellLifecycle;
    }

    public async Task HandleAsync(AppActivationArguments args)
    {
        try
        {
            await HandleCoreAsync(args).ConfigureAwait(false);
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

    private async Task HandleCoreAsync(AppActivationArguments args)
    {
        var activationItems = await ExtractActivationItemsAsync(args);

        if (activationItems.Count == 0)
            return;

        await _shellLifecycle.ShowAsync().ConfigureAwait(false);

        foreach (var item in activationItems)
        {
            if (item.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: здесь вызвать use case добавления magnet-ссылки
                await _shellLifecycle.OpenMagnetLinkAsync(item).ConfigureAwait(false);
                continue;
            }

            if (Path.GetExtension(item).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                await _shellLifecycle.OpenTorrentFileAsync(item).ConfigureAwait(false);
            }
        }
    }

    private static async Task<List<string>> ExtractActivationItemsAsync(AppActivationArguments args)
    {
        var result = new List<string>();

        if (args.Kind == ExtendedActivationKind.File &&
            args.Data is FileActivatedEventArgs fileArgs)
        {
            foreach (var item in fileArgs.Files)
            {
                if (item is StorageFile file &&
                    file.FileType.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(file.Path);
                }
            }

            return result;
        }

        // Для Inno/unpackaged-сценария:
        // Windows запускает приложение как NiTorrent.App.exe "%1".
        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            if (IsTorrentActivationArgument(arg))
                result.Add(arg);
        }

        return result;
    }

    private static bool IsTorrentActivationArgument(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            return true;

        return Path.GetExtension(value).Equals(".torrent", StringComparison.OrdinalIgnoreCase);
    }
}
