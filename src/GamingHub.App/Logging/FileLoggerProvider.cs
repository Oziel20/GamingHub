using Microsoft.Extensions.Logging;

namespace GamingHub.App.Logging;

/// <summary>Plain structured line log: timestamp, level, category, message, exception TYPE only (no stack traces).</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly object _lock = new();

    public FileLoggerProvider(string dir) { _dir = dir; Directory.CreateDirectory(dir); }
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);
    public void Dispose() { }

    internal void Write(string line)
    {
        lock (_lock)
        {
            try { File.AppendAllText(Path.Combine(_dir, $"log-{DateTime.UtcNow:yyyyMMdd}.txt"), line + Environment.NewLine); }
            catch (IOException) { /* logging must never crash the app */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category; private readonly FileLoggerProvider _p;
        public FileLogger(string category, FileLoggerProvider p) { _category = category; _p = p; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var line = $"{DateTime.UtcNow:O}\t{level}\t{_category}\t{formatter(state, null)}";
            if (ex is not null) line += $"\t{ex.GetType().Name}";
            _p.Write(line);
        }
    }
}
