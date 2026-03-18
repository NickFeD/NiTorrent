using Microsoft.Extensions.Logging;

namespace NiTorrent.App.Services;

internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _filePath;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public FileLogger(string categoryName, string filePath)
    {
        _categoryName = categoryName;
        _filePath = filePath;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}: {formatter(state, exception)}";
        if (exception is not null)
            message += Environment.NewLine + exception;

        WriteLine(message);
    }

    private void WriteLine(string message)
    {
        Gate.Wait();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.AppendAllText(_filePath, message + Environment.NewLine, System.Text.Encoding.UTF8);
        }
        finally
        {
            Gate.Release();
        }
    }
}
