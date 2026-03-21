using Microsoft.Windows.Storage.Pickers;
using NiTorrent.App.Services.Windowing;
using NiTorrent.Application.Abstractions;
using WinRT.Interop;

namespace NiTorrent.App.Services;

public sealed class WinPickerHelper(IMainWindowAccessor mainWindowAccessor) : IPickerHelper
{
    public async Task<string?> PickSingleFilePathAsync(params string[] fileTypes)
    {
        var window = mainWindowAccessor.Current ?? throw new InvalidOperationException("Main window is not initialized");
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

        var picker = new FileOpenPicker(windowId);

        if (fileTypes is null || fileTypes.Length == 0)
            fileTypes = ["*"];

        foreach (var ft in fileTypes)
            picker.FileTypeFilter.Add(ft);

        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }

    public async Task<string?> PickSingleFolderPathAsync(CancellationToken ct = default)
    {
        var window = mainWindowAccessor.Current ?? throw new InvalidOperationException("Main window is not initialized");
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

        var picker = new FolderPicker(windowId);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }
}
