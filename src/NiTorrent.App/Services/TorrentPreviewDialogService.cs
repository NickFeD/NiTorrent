using NiTorrent.App.Services.Windowing;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Services;

public sealed class TorrentPreviewDialogService(
    IServiceProvider services,
    IUiDispatcher uiDispatcher,
    IMainWindowAccessor mainWindowAccessor) : ITorrentPreviewDialogService
{
    private readonly IServiceProvider _services = services;
    private readonly IUiDispatcher _uiDispatcher = uiDispatcher;
    private readonly IMainWindowAccessor _mainWindowAccessor = mainWindowAccessor;

    public Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<TorrentPreviewDialogResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _uiDispatcher.EnqueueAsync(() =>
        {
            var vm = ActivatorUtilities.CreateInstance<TorrentPreviewViewModel>(_services, preview);
            var window = new Views.TorrentPreviewWindow(vm);

            void ClosedHandler(object? s, WindowEventArgs e)
            {
                window.Closed -= ClosedHandler;

                if (!window.Result)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                tcs.TrySetResult(new TorrentPreviewDialogResult(
                    SelectedFilePaths: window.SelectedFilePaths,
                    OutputFolder: vm.OutputFolder
                ));
            }

            window.Closed += ClosedHandler;

            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    _mainWindowAccessor.Current?.DispatcherQueue.TryEnqueue(() =>
                    {
                        try { window.Close(); } catch { }
                    });
                });
            }

            window.BringToFront();
            window.Activate();
        });

        return tcs.Task;
    }
}
