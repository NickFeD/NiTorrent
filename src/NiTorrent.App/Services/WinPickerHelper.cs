using Microsoft.Windows.Storage.Pickers;
using NiTorrent.App.Services.AppLifecycle;
using NiTorrent.Presentation.Abstractions;
using WinRT.Interop;

namespace NiTorrent.App.Services;

public sealed class WinPickerHelper(IAppShellLifecycle shellLifecycle) : IPickerHelper
{
    private readonly IAppShellLifecycle _shellLifecycle = shellLifecycle;

    public async Task<string?> PickSingleFilePathAsync(params string[] fileTypes)
    {
        var hwnd = WindowNative.GetWindowHandle(GetWindow());
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

        var picker = new FileOpenPicker(windowId);

        // если не передали фильтры - разрешаем все
        if (fileTypes is null || fileTypes.Length == 0)
            fileTypes = ["*"];

        foreach (var ft in fileTypes)
            picker.FileTypeFilter.Add(ft);

        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }

    public async Task<string?> PickSingleFolderPathAsync(CancellationToken ct = default)
    {
        var hwnd = WindowNative.GetWindowHandle(GetWindow());
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

        var picker = new FolderPicker(windowId);

        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }

    private Window GetWindow()
        => _shellLifecycle.CurrentWindow ?? throw new InvalidOperationException("Main window is not initialized");
}
