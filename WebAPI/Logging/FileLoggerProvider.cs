using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

namespace WebAPI.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDisposable? _onChangeToken;
    private readonly object _syncRoot = new();
    private FileLoggerOptions _options;
    private DateOnly _currentFileDate;

    public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
        _currentFileDate = DateOnly.FromDateTime(DateTime.UtcNow);
        _onChangeToken = optionsMonitor.OnChange(options => _options = options);
        CleanupOldFiles();
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, static (category, provider) => new FileLogger(category, provider), this);

    public void Dispose()
    {
        _loggers.Clear();
        _onChangeToken?.Dispose();
    }

    internal bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _options.MinimumLevel;

    internal void WriteLog(string categoryName, LogLevel logLevel, EventId eventId, string message, Exception? exception)
    {
        DateTime utcNow = DateTime.UtcNow;
        string line = FormatLogLine(utcNow, categoryName, logLevel, eventId, message, exception);

        lock (_syncRoot)
        {
            RotateIfNeeded(utcNow);

            string logDirectory = GetAbsoluteDirectoryPath();
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, $"{_options.FileNamePrefix}{utcNow:yyyyMMdd}.log");
            File.AppendAllText(logPath, line, Encoding.UTF8);
        }
    }

    private static string FormatLogLine(
        DateTime utcNow,
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append(utcNow.ToString("O"));
        builder.Append(" [");
        builder.Append(logLevel);
        builder.Append("] ");
        builder.Append(categoryName);
        builder.Append(" [");
        builder.Append(eventId.Id);
        builder.Append("] ");
        builder.AppendLine(message);

        if (exception is not null)
        {
            builder.AppendLine(exception.ToString());
        }

        return builder.ToString();
    }

    private void RotateIfNeeded(DateTime utcNow)
    {
        DateOnly nextDate = DateOnly.FromDateTime(utcNow);
        if (nextDate == _currentFileDate)
        {
            return;
        }

        _currentFileDate = nextDate;
        CleanupOldFiles();
    }

    private void CleanupOldFiles()
    {
        string logDirectory = GetAbsoluteDirectoryPath();
        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        if (_options.RetainedFileCountLimit <= 0)
        {
            return;
        }

        string searchPattern = $"{_options.FileNamePrefix}*.log";
        string[] files = Directory
            .GetFiles(logDirectory, searchPattern)
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToArray();

        foreach (string file in files.Skip(_options.RetainedFileCountLimit))
        {
            File.Delete(file);
        }
    }

    private string GetAbsoluteDirectoryPath() =>
        Path.IsPathRooted(_options.DirectoryPath)
            ? _options.DirectoryPath
            : Path.Combine(AppContext.BaseDirectory, _options.DirectoryPath);
}
