using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentUseCase(
    ITorrentCollectionRepository collectionRepository,
    ITorrentWriteService writeService,
    ITorrentSourceStore sourceStore)
{
    public async Task<TorrentId> ExecuteAsync(AddTorrentRequest request, CancellationToken ct = default)
    {
        var existing = await collectionRepository.GetAllAsync(ct).ConfigureAwait(false);
        var duplicate = TorrentDuplicatePolicy.FindDuplicate(
            existing,
            request.PreparedSource.Key,
            request.PreparedSource.Name,
            request.SavePath);

        if (duplicate is not null)
            throw new UserVisibleException("Этот торрент уже добавлен в приложение.");

        var id = new TorrentId(Guid.NewGuid());
        var selectedFiles = NormalizeSelectedFiles(request.SelectedFilePaths);
        var now = DateTimeOffset.UtcNow;

        await sourceStore.SaveAsync(id, request.PreparedSource.Key, request.PreparedSource.TorrentBytes, ct).ConfigureAwait(false);

        var entry = new TorrentEntry(
            id,
            request.PreparedSource.Key,
            request.PreparedSource.Name,
            request.PreparedSource.TotalSize,
            request.SavePath,
            now,
            TorrentIntent.Running,
            TorrentLifecycleState.WaitingForEngine,
            new TorrentRuntimeState(
                TorrentLifecycleState.WaitingForEngine,
                IsComplete: false,
                Progress: 0,
                DownloadRateBytesPerSecond: 0,
                UploadRateBytesPerSecond: 0,
                Error: null,
                IsEngineBacked: false),
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0, Source: TorrentStatusSource.Cached),
            request.PreparedSource.HasMetadata,
            selectedFiles,
            PerTorrentSettings: null,
            DeferredActions: Array.Empty<DeferredAction>());

        await collectionRepository.UpsertAsync(entry, ct).ConfigureAwait(false);
        await collectionRepository.SaveAsync(ct: ct).ConfigureAwait(false);

        try
        {
            var runtime = await writeService.AddAsync(id, request, ct).ConfigureAwait(false);
            entry = entry.WithRuntime(runtime);
        }
        catch
        {
            entry = entry.WithDeferredActions(
                DeferredActionPolicy.Merge(
                    entry.DeferredActions,
                    new DeferredAction(DeferredActionType.Start, now)));
            entry = entry.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(entry));
        }

        await collectionRepository.UpsertAsync(entry, ct).ConfigureAwait(false);
        await collectionRepository.SaveAsync(ct: ct).ConfigureAwait(false);
        return id;
    }

    private static IReadOnlyList<string> NormalizeSelectedFiles(IReadOnlySet<string>? selectedFilePaths)
        => selectedFilePaths is { Count: > 0 }
            ? selectedFilePaths
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();

}
