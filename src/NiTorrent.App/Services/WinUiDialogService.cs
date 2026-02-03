using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class WinUiDialogService : IDialogService
{
    public async Task ShowTextAsync(string title, string text, CancellationToken ct = default)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = text, Margin = new Thickness(10) },
                Margin = new Thickness(10)
            },
            Margin = new Thickness(10),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
