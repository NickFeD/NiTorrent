namespace NiTorrent.Presentation.Abstractions;

public interface IUiDispatcher
{
    /// <summary>Выполнить на UI-потоке. Возвращает false если не удалось поставить в очередь.</summary>
    bool TryEnqueue(Action action);

    /// <summary>Выполнить на UI-потоке и дождаться завершения.</summary>
    Task EnqueueAsync(Action action, CancellationToken ct = default);
}
