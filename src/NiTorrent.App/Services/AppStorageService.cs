using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class AppStorageService : IAppStorageService
{
    private readonly string _localRoot;
    private readonly string _cacheRoot;

    public AppStorageService()
    {
        // packaged friendly, но не падаем, если нет package identity
        try
        {
            _localRoot = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            _cacheRoot = Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path;
        }
        catch
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NiTorrent");

            _localRoot = Path.Combine(baseDir, "LocalState");
            _cacheRoot = Path.Combine(baseDir, "LocalCache");
        }
    }

    public string GetLocalPath(string relative) => Path.Combine(_localRoot, relative);
    public string GetCachePath(string relative) => Path.Combine(_cacheRoot, relative);

    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public void EnsureParentDirectory(string filePath)
        => Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
}
