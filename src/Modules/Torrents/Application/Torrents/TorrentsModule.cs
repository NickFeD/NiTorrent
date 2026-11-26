using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Domain.Torrents;
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentsModule(ITorrentRepository repository, ITorrentSession session,
    ITorrentMetadataProvider metadata, ITorrentDownloadFactory factory,
    SetAllTorrentStatesUseCase bulk) : ITorrentsModule, ITorrentsLifetime
{
    public event Action? EngineStateChanged
    {
        add => session.EngineStateChanged += value;
        remove => session.EngineStateChanged -= value;
    }
    public bool IsEngineReady => session.IsEngineReady;
    public string? EngineError => session.EngineError;
    public async Task<IReadOnlyList<TorrentListItem>> GetAllAsync(CancellationToken ct = default) =>
        Array.AsReadOnly((await repository.GetAllAsync(ct).ConfigureAwait(false)).Select(Map).ToArray());
    public async Task<TorrentPreview> PreviewAsync(TorrentSource source, CancellationToken ct = default)
    {
        var preview = await new PreviewTorrentContentsUseCase(metadata, repository)
            .ExecuteAsync(new() { Source = CopySource(source) }, ct).ConfigureAwait(false);
        return preview with { Files = Array.AsReadOnly(preview.Files.Select(CopyFile).ToArray()) };
    }
    public async Task<TorrentListItem> AddAsync(TorrentSource source, string directory,
        IReadOnlyList<TorrentFileEntry> files, CancellationToken ct = default)
    {
        var result = await new CreateTorrentDownloadUseCase(session, factory, repository, metadata)
            .ExecuteAsync(new(CopySource(source), directory, files.Select(CopyFile).ToList()), ct).ConfigureAwait(false);
        return Map(result.TorrentDownload);
    }
    public async Task StartAsync(Guid id, CancellationToken ct = default) =>
        await new StartTorrentUseCase(repository, session).ExecuteAsync(new(id), ct).ConfigureAwait(false);
    public async Task PauseAsync(Guid id, CancellationToken ct = default) =>
        await new PauseTorrentUseCase(repository, session).ExecuteAsync(new(id), ct).ConfigureAwait(false);
    public async Task DeleteAsync(Guid id, bool deleteFiles, CancellationToken ct = default) =>
        await new DeleteTorrentUseCase(repository, session).ExecuteAsync(new(id, deleteFiles), ct).ConfigureAwait(false);
    public async Task<BulkTorrentOutcome> SetAllAsync(bool running, CancellationToken ct = default)
    {
        var result = await bulk.ExecuteAsync(running, ct).ConfigureAwait(false);
        return new(result.Applied, Array.AsReadOnly(result.Failed.ToArray()));
    }
    public void Retry(Guid id) => session.Retry(id);
    public void RequestStop() => session.RequestStop();
    private static TorrentListItem Map(TorrentDownload item) =>
        new(item.Id, item.InfoHash, item.Name, item.SavePath, item.Size, item.Status);
    private static TorrentFileEntry CopyFile(TorrentFileEntry file) => new(file.FullPath, file.SizeByte, file.IsSelected);
    private static TorrentSource CopySource(TorrentSource source) => source is TorrentSource.TorrentBytes bytes
        ? new TorrentSource.TorrentBytes(bytes.Bytes.ToArray()) : source;
}
