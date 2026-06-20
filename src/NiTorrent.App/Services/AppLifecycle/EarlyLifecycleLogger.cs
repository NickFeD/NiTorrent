using System.Diagnostics;
using NiTorrent.App.Services;

namespace NiTorrent.App.Services.AppLifecycle;

public static class EarlyLifecycleLogger
{
    public static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [Information] Lifecycle: [Lifecycle] {message}";
        Debug.WriteLine(line);

        try
        {
            var storage = new AppStorageService();
            var path = storage.GetLocalPath(Path.Combine("Logs", $"app-{DateTime.Now:yyyyMMdd}.log"));
            storage.EnsureParentDirectory(path);
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
            // Best-effort diagnostics before the host and configured logging exist.
        }
    }
}
