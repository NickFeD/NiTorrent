using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentSourceResolver(
    TorrentStableKeyAccessor stableKeyAccessor,
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentEventOrchestrator eventOrchestrator) : ITorrentSourcePreparationService
{
    private ClientEngine Engine
        => startupCoordinator.Engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public Task<PreparedTorrentSource> PrepareAsync(TorrentSource source, CancellationToken ct = default)
        => ResolveAsync(source, EnsureStartedAsync, () => Engine, ct);

    public async Task<PreparedTorrentSource> ResolveAsync(
        TorrentSource source,
        Func<CancellationToken, Task> ensureStartedAsync,
        Func<ClientEngine> getEngine,
        CancellationToken ct)
    {
        return source switch
        {
            TorrentSource.TorrentFile tf => await PrepareFromFileAsync(tf.Path, ct).ConfigureAwait(false),
            TorrentSource.Magnet m => await PrepareFromMagnetAsync(m.Uri, ensureStartedAsync, getEngine, ct).ConfigureAwait(false),
            TorrentSource.TorrentBytes b => await PrepareFromBytesAsync(b.Bytes).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
    }

    private Task EnsureStartedAsync(CancellationToken ct = default)
        => startupCoordinator.EnsureStartedAsync(runtimeContext.OperationGate, eventOrchestrator.RaiseLoaded, ct);

    private async Task<PreparedTorrentSource> PrepareFromFileAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        return await PrepareFromBytesAsync(bytes).ConfigureAwait(false);
    }

    private async Task<PreparedTorrentSource> PrepareFromMagnetAsync(
        string magnetUri,
        Func<CancellationToken, Task> ensureStartedAsync,
        Func<ClientEngine> getEngine,
        CancellationToken ct)
    {
        await ensureStartedAsync(ct).ConfigureAwait(false);
        var magnet = MagnetLink.Parse(magnetUri);
        var metadata = await getEngine().DownloadMetadataAsync(magnet, ct).ConfigureAwait(false);
        return await PrepareFromBytesAsync(metadata.ToArray()).ConfigureAwait(false);
    }

    private async Task<PreparedTorrentSource> PrepareFromBytesAsync(byte[] bytes)
    {
        var torrent = await Torrent.LoadAsync(bytes).ConfigureAwait(false);
        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new PreparedTorrentSource(
            bytes,
            new TorrentKey(stableKeyAccessor.GetStableKey(torrent)),
            torrent.Name,
            torrent.Size,
            files,
            HasMetadata: true);
    }
}
