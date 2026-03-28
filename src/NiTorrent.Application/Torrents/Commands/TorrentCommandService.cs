using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Commands;

public sealed class TorrentCommandService(
    ITorrentCollectionRepository collectionRepository,
    ITorrentEngineGateway engineGateway) : ITorrentCommandService
{
    public Task<TorrentCommandResult> StartAsync(TorrentId id, CancellationToken ct = default) =>
        ExecuteAsync(id, deleteData: false, CommandType.Start, ct);

    public Task<TorrentCommandResult> PauseAsync(TorrentId id, CancellationToken ct = default) =>
        ExecuteAsync(id, deleteData: false, CommandType.Pause, ct);

    public Task<TorrentCommandResult> RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default) =>
        ExecuteAsync(id, deleteData, CommandType.Remove, ct);

    private async Task<TorrentCommandResult> ExecuteAsync(TorrentId id, bool deleteData, CommandType commandType, CancellationToken ct)
    {
        var entry = await collectionRepository.TryGetAsync(id, ct).ConfigureAwait(false);
        if (entry is null)
            return TorrentCommandResult.NotFound(id);

        var now = DateTimeOffset.UtcNow;
        var updated = commandType switch
        {
            CommandType.Start => TorrentEntryCommandPolicy.RequestStart(entry, now),
            CommandType.Pause => TorrentEntryCommandPolicy.RequestPause(entry, now),
            CommandType.Remove => TorrentEntryCommandPolicy.RequestRemove(entry, deleteData, now),
            _ => entry
        };

        await collectionRepository.UpsertAsync(updated, ct).ConfigureAwait(false);
        await collectionRepository.SaveAsync(ct).ConfigureAwait(false);

        bool appliedImmediately;
        try
        {
            appliedImmediately = commandType switch
            {
                CommandType.Start => await engineGateway.StartAsync(id, ct).ConfigureAwait(false),
                CommandType.Pause => await engineGateway.PauseAsync(id, ct).ConfigureAwait(false),
                CommandType.Remove => await engineGateway.RemoveAsync(id, deleteData, ct).ConfigureAwait(false),
                _ => false
            };
        }
        catch
        {
            appliedImmediately = false;
        }

        if (!appliedImmediately)
        {
            return TorrentCommandResult.Deferred(id, commandType switch
            {
                CommandType.Start => "Команда запуска сохранена и будет применена, когда движок будет готов.",
                CommandType.Pause => "Команда паузы сохранена и будет применена, когда движок будет готов.",
                CommandType.Remove => "Команда удаления сохранена и будет применена, когда движок будет готов.",
                _ => null
            });
        }

        if (commandType == CommandType.Remove)
        {
            await collectionRepository.RemoveAsync(id, ct).ConfigureAwait(false);
            await collectionRepository.SaveAsync(ct).ConfigureAwait(false);
            return TorrentCommandResult.Success(id);
        }

        var finalized = FinalizeAppliedExecution(updated, commandType);
        await collectionRepository.UpsertAsync(finalized, ct).ConfigureAwait(false);
        await collectionRepository.SaveAsync(ct).ConfigureAwait(false);

        return TorrentCommandResult.Success(id);
    }

    private static TorrentEntry FinalizeAppliedExecution(TorrentEntry entry, CommandType commandType)
    {
        var remaining = commandType switch
        {
            CommandType.Start => entry.DeferredActions.Where(x => x.Type != DeferredActionType.Start).ToList(),
            CommandType.Pause => entry.DeferredActions.Where(x => x.Type != DeferredActionType.Pause).ToList(),
            _ => entry.DeferredActions.ToList()
        };

        var updated = entry.WithDeferredActions(remaining);
        return updated.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(updated));
    }

    private enum CommandType
    {
        Start,
        Pause,
        Remove
    }
}
