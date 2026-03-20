namespace NiTorrent.Application.Abstractions;

public interface IFolderLauncher
{
    Task OpenAsync(string path, CancellationToken ct = default);
}
