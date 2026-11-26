namespace NiTorrent.App.Services.AppLifecycle;

public interface IAppShellLifecycle
{
    Window? CurrentWindow { get; }

    Task StartAsync(CancellationToken ct = default);

    Task ShowAsync();

    Task HideToTrayAsync();

    Task CloseAsync();

    Task OpenTorrentFileAsync(string filePath);

    Task OpenMagnetLinkAsync(string magnetLink);
}
