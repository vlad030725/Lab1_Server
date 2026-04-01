namespace WebAPI.Logging;

public sealed class FileLoggerOptions
{
    public string DirectoryPath { get; set; } = "logs";
    public string FileNamePrefix { get; set; } = "webapi-";
    public int RetainedFileCountLimit { get; set; } = 7;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
