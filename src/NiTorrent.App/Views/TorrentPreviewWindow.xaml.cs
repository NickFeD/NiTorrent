using Microsoft.UI.Windowing;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Torrents;
using NiTorrent.Presentation.Features.Torrents.Tree;
using WinUIEx;

namespace NiTorrent.App.Views;

public sealed partial class TorrentPreviewWindow : WindowEx
{

    private readonly TaskCompletionSource<TorrentPreviewDialogResult?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TorrentPreviewViewModel ViewModel { get; }

    public List<TorrentFileEntry> Files => ViewModel.GetFiles();

    public TorrentPreviewWindow(TorrentPreviewViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
        Root.DataContext = ViewModel;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        Closed += OnClosed;
    }
    private async void FilesTreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        // В режиме ItemsSource TreeViewExpandingEventArgs.Item содержит data item.
        if (args.Item is TorrentTreeItemViewModel node)
            await node.EnsureChildrenLoadedAsync();
    }


    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(new TorrentPreviewDialogResult(
            SelectedFiles: Files,
            OutputFolder: ViewModel.OutputFolder));

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(null);
        Close();
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");

        // WinUI 3 requires this:
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ViewModel.OutputFolder = folder.Path;
        }
    }


    public Task<TorrentPreviewDialogResult?> WaitForResultAsync()
       => _tcs.Task;

    private void OnClosed(object sender, WindowEventArgs e)
    {
        Closed -= OnClosed;
        _tcs.TrySetResult(null);
    }
}

