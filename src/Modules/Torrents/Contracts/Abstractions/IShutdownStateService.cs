namespace NiTorrent.Application.Abstractions;

public interface IShutdownStateService
{
    Task<PreviousShutdownState> ReadPreviousStateAsync(CancellationToken cancellationToken);

    Task MarkStartedAsync(CancellationToken cancellationToken);

    Task MarkCleanShutdownAsync(CancellationToken cancellationToken);
}
