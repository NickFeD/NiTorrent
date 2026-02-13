using NiTorrent.Application.Torrents;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Services;

public sealed class TorrentPreviewDialogService(IServiceProvider services, IUiDispatcher uiDispatcher) : ITorrentPreviewDialogService
{
    private readonly IServiceProvider _services = services;
    private readonly IUiDispatcher _uiDispatcher = uiDispatcher;

    public Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct = default)
    {

        var tcs = new TaskCompletionSource<TorrentPreviewDialogResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _uiDispatcher.EnqueueAsync(() =>
        {
            // создаём VM через DI, передавая preview параметром
            var vm = ActivatorUtilities.CreateInstance<TorrentPreviewViewModel>(_services, preview);
            TorrentPreviewWindow window;
            try
            {
                window = new Views.TorrentPreviewWindow(vm);
            }
            catch (Exception e)
            {

                throw e;
            }


            // окно — чистая WinUI-деталь (только App слой)

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

            // необязательно, но приятно: отмена -> закрыть окно
            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    // закрытие окна должно быть на UI thread
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        try { window.Close(); } catch { /* ignore */ }
                    });
                });
            }
            window.BringToFront();
            window.Activate();


        });

        return tcs.Task;
    }
}
