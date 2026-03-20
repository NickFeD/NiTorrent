using Microsoft.Extensions.Logging;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class BackgroundTaskRunner
{
    private readonly ILogger<BackgroundTaskRunner> _logger;

    public BackgroundTaskRunner(ILogger<BackgroundTaskRunner> logger)
    {
        _logger = logger;
    }

    public void Run(Task task, string name)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is not null)
                _logger.LogError(t.Exception, "Background task failed: {Name}", name);
        }, TaskScheduler.Default);
    }
}
