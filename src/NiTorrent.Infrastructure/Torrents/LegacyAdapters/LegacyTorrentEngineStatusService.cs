using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

public sealed class LegacyTorrentEngineStatusService : ITorrentEngineStatusService
{
    private readonly ITorrentService _torrentService;

    public LegacyTorrentEngineStatusService(ITorrentService torrentService)
    {
        _torrentService = torrentService;
        _torrentService.Loaded += OnLoaded;
    }

    public event Action? Loaded;

    public Task InitializeAsync(CancellationToken ct = default) => _torrentService.InitializeAsync(ct);

    private void OnLoaded() => Loaded?.Invoke();
}
