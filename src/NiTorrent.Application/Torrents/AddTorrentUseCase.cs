using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentUseCase(
    ITorrentCollectionRepository collectionRepository,
    ITorrentWriteService writeService)
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
        var runtime = await writeService.AddAsync(id, request, ct).ConfigureAwait(false);
        var selectedFiles = NormalizeSelectedFiles(request.SelectedFilePaths);

        var entry = new TorrentEntry(
            id,
            request.PreparedSource.Key,
            request.PreparedSource.Name,
            request.PreparedSource.TotalSize,
            request.SavePath,
            DateTimeOffset.UtcNow,
            TorrentIntent.Running,
            runtime.LifecycleState,
            runtime,
            BuildStatus(runtime),
            request.PreparedSource.HasMetadata,
            selectedFiles,
            PerTorrentSettings: null,
            DeferredActions: Array.Empty<DeferredAction>());

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

    private static TorrentStatus BuildStatus(TorrentRuntimeState runtime)
        => new(
            TorrentLifecycleStateMapper.ToPhase(runtime.LifecycleState),
            runtime.IsComplete,
            runtime.Progress,
            runtime.DownloadRateBytesPerSecond,
            runtime.UploadRateBytesPerSecond,
            runtime.Error,
            runtime.IsEngineBacked ? TorrentStatusSource.Live : TorrentStatusSource.Cached);
}
