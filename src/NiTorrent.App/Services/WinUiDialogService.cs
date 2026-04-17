using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services;

public sealed class WinUiDialogService(IUiDispatcher uiDispatcher) : IDialogService
{
    private readonly IUiDispatcher _uiDispatcher = uiDispatcher;

    public Task ShowTextAsync(string title, string text, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_uiDispatcher.TryEnqueue(async () =>
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(ct);
                        return;
                    }

                    var xamlRoot = App.MainWindow?.Content?.XamlRoot;
                    if (xamlRoot is null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    var dialog = new ContentDialog
                    {
                        Title = title,
                        CloseButtonText = "Закрыть",
                        DefaultButton = ContentDialogButton.Close,
                        Content = new ScrollViewer
                        {
                            Content = new TextBlock
                            {
                                Text = text,
                                Margin = new Thickness(10),
                                TextWrapping = TextWrapping.Wrap
                            },
                            Margin = new Thickness(10)
                        },
                        Margin = new Thickness(10),
                        XamlRoot = xamlRoot
                    };

                    await dialog.ShowAsync();
                    tcs.TrySetResult(null);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue dialog on UI thread."));
        }

        return tcs.Task;
    }

    public Task<WindowCloseChoice?> ShowWindowCloseChoiceAsync(bool defaultMinimizeToTray, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<WindowCloseChoice?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_uiDispatcher.TryEnqueue(async () =>
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(ct);
                        return;
                    }

                    var xamlRoot = App.MainWindow?.Content?.XamlRoot;
                    if (xamlRoot is null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    var rememberCheckBox = new CheckBox
                    {
                        Content = "Запомнить мой выбор",
                        IsChecked = false,
                        Margin = new Thickness(0, 12, 0, 0)
                    };

                    var description = new TextBlock
                    {
                        Text = "При нажатии на крестик приложение можно либо полностью закрыть, либо свернуть в трей.",
                        TextWrapping = TextWrapping.Wrap
                    };

                    var dialog = new ContentDialog
                    {
                        Title = "Закрыть приложение?",
                        PrimaryButtonText = defaultMinimizeToTray ? "Свернуть в трей" : "Закрыть приложение",
                        SecondaryButtonText = defaultMinimizeToTray ? "Закрыть приложение" : "Свернуть в трей",
                        CloseButtonText = "Отмена",
                        DefaultButton = ContentDialogButton.Primary,
                        Content = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                description,
                                rememberCheckBox
                            }
                        },
                        XamlRoot = xamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    WindowCloseChoice? choice = result switch
                    {
                        ContentDialogResult.Primary => new WindowCloseChoice(
                            defaultMinimizeToTray ? WindowCloseAction.MinimizeToTray : WindowCloseAction.ExitApplication,
                            rememberCheckBox.IsChecked == true),
                        ContentDialogResult.Secondary => new WindowCloseChoice(
                            defaultMinimizeToTray ? WindowCloseAction.ExitApplication : WindowCloseAction.MinimizeToTray,
                            rememberCheckBox.IsChecked == true),
                        _ => null
                    };

                    tcs.TrySetResult(choice);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue dialog on UI thread."));
        }

        return tcs.Task;
    }
}
