using Microsoft.Extensions.Logging;

namespace Darktrace.WindowsEventForwarder.Infrastructure;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _sync = new();

    public FileLoggerProvider(string path) => _path = path;

    public ILogger CreateLogger(string categoryName) => new FileLogger(_path, categoryName, _sync);

    public void Dispose() { }

    private sealed class FileLogger(string path, string category, object sync) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var line = $"{DateTimeOffset.Now:O} {logLevel,-11} {category}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;

            lock (sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
    }
}
