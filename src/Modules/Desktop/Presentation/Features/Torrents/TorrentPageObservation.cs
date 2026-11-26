namespace NiTorrent.Presentation.Features.Torrents;

/// <summary>Owns page observation across navigation and tray transitions, preserving local state while hidden.</summary>
public sealed class TorrentPageObservation(TorrentViewModel viewModel, DesktopObservationLifetime visibility) : IDisposable
{
    private bool _active;
    private CancellationTokenSource? _loading;
    public async Task ActivateAsync()
    {
        if (!_active)
        {
            _active = true;
            visibility.VisibilityChanged += OnVisibilityChanged;
        }
        if (visibility.IsVisible) await LoadAsync();
    }
    public void Dispose()
    {
        if (!_active) return;
        _active = false;
        visibility.VisibilityChanged -= OnVisibilityChanged;
        CancelLoading();
        viewModel.TorrentUnloaded();
    }
    private void CancelLoading()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
    }
    private async void OnVisibilityChanged(bool visible)
    {
        if (!visible)
        {
            CancelLoading();
            viewModel.SuspendObservation();
        }
        else await LoadAsync();
    }
    private async Task LoadAsync()
    {
        CancelLoading();
        _loading = new();
        try { await viewModel.TorrentLoading(_loading.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_active && visibility.IsVisible) viewModel.StatusText = $"Не удалось загрузить коллекцию: {ex.Message}";
        }
    }
}
