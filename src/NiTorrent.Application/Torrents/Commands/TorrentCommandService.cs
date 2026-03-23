using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Commands;

public sealed class TorrentCommandService(
    ITorrentCollectionRepository collectionRepository,
    ITorrentRuntimeFactsProvider runtimeFactsProvider,
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

        var engineReady = HasRuntimeFact(entry, runtimeFactsProvider.GetAll());
        var now = DateTimeOffset.UtcNow;

        var updated = commandType switch
        {
            CommandType.Start => TorrentEntryCommandPolicy.RequestStart(entry, now, engineReady),
            CommandType.Pause => TorrentEntryCommandPolicy.RequestPause(entry, now, engineReady),
            CommandType.Remove => TorrentEntryCommandPolicy.RequestRemove(entry, deleteData, now, engineReady),
            _ => entry
        };

        if (commandType == CommandType.Remove && engineReady)
        {
            try
            {
                await engineGateway.RemoveAsync(id, deleteData, ct).ConfigureAwait(false);
                await collectionRepository.RemoveAsync(id, ct).ConfigureAwait(false);
                await collectionRepository.SaveAsync(ct).ConfigureAwait(false);
                return TorrentCommandResult.Success(id);
            }
            catch
            {
                return TorrentCommandResult.Failed(id, "Не удалось удалить торрент.");
            }
        }

        await collectionRepository.UpsertAsync(updated, ct).ConfigureAwait(false);
        await collectionRepository.SaveAsync(ct).ConfigureAwait(false);

        if (!engineReady)
        {
            return TorrentCommandResult.Deferred(id, commandType switch
            {
                CommandType.Start => "Команда запуска сохранена и будет применена после готовности движка.",
                CommandType.Pause => "Команда паузы сохранена и будет применена после готовности движка.",
                CommandType.Remove => "Команда удаления сохранена и будет применена после готовности движка.",
                _ => null
            });
        }

        try
        {
            switch (commandType)
            {
                case CommandType.Start:
                    await engineGateway.StartAsync(id, ct).ConfigureAwait(false);
                    break;
                case CommandType.Pause:
                    await engineGateway.PauseAsync(id, ct).ConfigureAwait(false);
                    break;
            }
        }
        catch
        {
            return TorrentCommandResult.Failed(id, commandType switch
            {
                CommandType.Start => "Не удалось запустить торрент.",
                CommandType.Pause => "Не удалось поставить торрент на паузу.",
                CommandType.Remove => "Не удалось удалить торрент.",
                _ => "Не удалось выполнить команду."
            });
        }

        return TorrentCommandResult.Success(id);
    }

    private static bool HasRuntimeFact(TorrentEntry entry, IReadOnlyList<TorrentRuntimeFact> facts)
    {
        foreach (var fact in facts)
        {
            if (fact.Id is TorrentId id && id == entry.Id)
                return true;

            if (!entry.Key.IsEmpty && !fact.Key.IsEmpty &&
                string.Equals(entry.Key.Value, fact.Key.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private enum CommandType
    {
        Start,
        Pause,
        Remove
    }
}
