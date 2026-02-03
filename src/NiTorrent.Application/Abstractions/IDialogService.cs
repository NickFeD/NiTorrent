namespace NiTorrent.Application.Abstractions;

public interface IDialogService
{
    Task ShowTextAsync(string title, string text, CancellationToken ct = default);
}
