using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentEngineStatusService : ITorrentEngineStatusService, IDisposable
{
    private readonly ITorrentService _torrentService;
    private bool _isReady;

    public TorrentEngineStatusService(ITorrentService torrentService)
    {
        _torrentService = torrentService;
        _torrentService.Loaded += OnLoaded;
    }

    public event Action? Ready;

    public bool IsReady => _isReady;

    public Task InitializeAsync(CancellationToken ct = default)
        => _torrentService.InitializeAsync(ct);

    private void OnLoaded()
    {
        _isReady = true;
        Ready?.Invoke();
    }

    public void Dispose()
    {
        _torrentService.Loaded -= OnLoaded;
    }
}
