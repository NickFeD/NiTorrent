namespace NiTorrent.Application.Abstractions;

public interface IAppStorageService
{
    string GetLocalPath(string relative);
    string GetCachePath(string relative);
    void EnsureDirectory(string path);
    void EnsureParentDirectory(string filePath);
}
