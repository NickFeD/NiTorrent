using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentCommandUseCase(ITorrentCommandService commandService)
{
    public async Task ExecuteAsync(
        TorrentCommandType commandType,
        TorrentId id,
        bool deleteData = false,
        CancellationToken ct = default)
    {
        var result = commandType switch
        {
            TorrentCommandType.Start => await commandService.StartAsync(id, ct).ConfigureAwait(false),
            TorrentCommandType.Pause => await commandService.PauseAsync(id, ct).ConfigureAwait(false),
            TorrentCommandType.Remove => await commandService.RemoveAsync(id, deleteData, ct).ConfigureAwait(false),
            _ => TorrentCommandResult.Failed(id, "Unsupported torrent command.")
        };

        EnsureSucceeded(result, id, commandType);
    }

    private static void EnsureSucceeded(TorrentCommandResult result, TorrentId id, TorrentCommandType commandType)
    {
        if (result.Outcome is TorrentCommandOutcome.Success or TorrentCommandOutcome.Deferred)
            return;

        var fallback = commandType switch
        {
            TorrentCommandType.Start => "Не удалось запустить торрент.",
            TorrentCommandType.Pause => "Не удалось поставить торрент на паузу.",
            TorrentCommandType.Remove => "Не удалось удалить торрент.",
            _ => "Не удалось выполнить команду торрента."
        };

        throw new InvalidOperationException(result.Message ?? $"{fallback} ({id.Value})");
    }
}
