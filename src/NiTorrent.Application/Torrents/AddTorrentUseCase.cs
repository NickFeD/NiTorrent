using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentUseCase(
    ITorrentCollectionRepository collectionRepository,
    ITorrentWriteService writeService,
    ITorrentSourceStore sourceStore)
{
    public async Task<AddTorrentResult> ExecuteAsync(AddTorrentRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (request.PreparedSource.TorrentBytes.Length == 0)
            return AddTorrentResult.InvalidInput("Torrent source payload is empty.");

        if (string.IsNullOrWhiteSpace(request.PreparedSource.Name))
            return AddTorrentResult.InvalidInput("Torrent name cannot be empty.");

        SavePath savePath;
        try
        {
            savePath = new SavePath(request.SavePath);
        }
        catch (ArgumentException)
        {
            return AddTorrentResult.InvalidInput("Save path cannot be empty.");
        }

        var existing = await collectionRepository.GetAllAsync(ct).ConfigureAwait(false);
        var duplicate = TorrentDuplicatePolicy.FindDuplicate(
            existing,
            request.PreparedSource.Key,
            request.PreparedSource.Name,
            savePath);

        if (duplicate is not null)
            return AddTorrentResult.AlreadyExists("Этот торрент уже добавлен в приложение.");

        var id = TorrentId.New();
        var selectedFiles = NormalizeSelectedFiles(request.SelectedFilePaths);
        var now = DateTimeOffset.UtcNow;

        var entry = new TorrentEntry(
            id,
            request.PreparedSource.Key,
            request.PreparedSource.Name,
            request.PreparedSource.TotalSize,
            savePath,
            now,
            TorrentIntent.Running,
            TorrentLifecycleState.WaitingForEngine,
            TorrentRuntimeState.WaitingForEngine(0, false),
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0, Source: TorrentStatusSource.Cached),
            request.PreparedSource.HasMetadata,
            selectedFiles,
            PerTorrentSettings: null,
            DeferredActions: []);

        try
        {
            await collectionRepository.UpsertAsync(entry, ct).ConfigureAwait(false);
            await collectionRepository.SaveAsync(ct: ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return AddTorrentResult.StorageError("Не удалось сохранить торрент в каталоге.");
        }
        catch (UnauthorizedAccessException)
        {
            return AddTorrentResult.StorageError("Нет прав для сохранения торрента в каталоге.");
        }
        catch (InvalidOperationException)
        {
            return AddTorrentResult.StorageError("Каталог торрентов временно недоступен.");
        }

        try
        {
            await sourceStore.SaveAsync(id, request.PreparedSource.Key, request.PreparedSource.TorrentBytes, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            await RollbackEntryAsync(id, ct).ConfigureAwait(false);
            return AddTorrentResult.StorageError("Не удалось сохранить source-файл торрента.");
        }
        catch (UnauthorizedAccessException)
        {
            await RollbackEntryAsync(id, ct).ConfigureAwait(false);
            return AddTorrentResult.StorageError("Нет прав для сохранения source-файла торрента.");
        }

        try
        {
            var runtime = await writeService.AddAsync(id, request, ct).ConfigureAwait(false);
            entry = entry.WithRuntime(runtime);
        }
        catch (InvalidOperationException)
        {
            entry = entry.WithDeferredActions(
                DeferredActionPolicy.Merge(entry.DeferredActions, new DeferredAction(DeferredActionType.Start, now)));
            entry = entry.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(entry));
        }
        catch (IOException)
        {
            entry = entry.WithDeferredActions(
                DeferredActionPolicy.Merge(entry.DeferredActions, new DeferredAction(DeferredActionType.Start, now)));
            entry = entry.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(entry));
        }

        try
        {
            await collectionRepository.UpsertAsync(entry, ct).ConfigureAwait(false);
            await collectionRepository.SaveAsync(ct: ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            await RollbackEntryAsync(id, ct).ConfigureAwait(false);
            await sourceStore.DeleteAsync(id, ct).ConfigureAwait(false);
            return AddTorrentResult.StorageError("Не удалось зафиксировать состояние добавленного торрента.");
        }
        catch (UnauthorizedAccessException)
        {
            await RollbackEntryAsync(id, ct).ConfigureAwait(false);
            await sourceStore.DeleteAsync(id, ct).ConfigureAwait(false);
            return AddTorrentResult.StorageError("Нет прав для фиксации состояния добавленного торрента.");
        }
        catch (InvalidOperationException)
        {
            await RollbackEntryAsync(id, ct).ConfigureAwait(false);
            await sourceStore.DeleteAsync(id, ct).ConfigureAwait(false);
            return AddTorrentResult.StorageError("Каталог торрентов временно недоступен.");
        }

        return AddTorrentResult.Success(id);
    }

    private async Task RollbackEntryAsync(TorrentId id, CancellationToken ct)
    {
        try
        {
            await collectionRepository.RemoveAsync(id, ct).ConfigureAwait(false);
            await collectionRepository.SaveAsync(ct: ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Best effort rollback: caller already returns storage failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort rollback: caller already returns storage failure.
        }
        catch (InvalidOperationException)
        {
            // Best effort rollback: caller already returns storage failure.
        }
    }

    private static IReadOnlyList<string> NormalizeSelectedFiles(IReadOnlySet<string>? selectedFilePaths)
        => selectedFilePaths is { Count: > 0 }
            ? selectedFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
}
