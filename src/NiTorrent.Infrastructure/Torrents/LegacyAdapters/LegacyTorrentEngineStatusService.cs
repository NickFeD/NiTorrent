using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

/// <summary>
/// Transition-only status service over legacy ITorrentService.
/// </summary>
public sealed class LegacyTorrentEngineStatusService : ITorrentEngineStatusService, IDisposable
{
    private readonly ITorrentService _torrentService;
    private bool _isReady;

    public LegacyTorrentEngineStatusService(ITorrentService torrentService)
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
