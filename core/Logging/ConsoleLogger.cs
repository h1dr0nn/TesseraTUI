using System;

namespace Tessera.Core.Logging;

public class ConsoleLogger : ILogger
{
    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message, Exception? exception = null)
    {
        var combined = exception == null ? message : $"{message}: {exception.Message}";
        Write("ERROR", combined);
    }

    private void Write(string level, string message)
    {
        Console.WriteLine($"[{DateTimeOffset.Now:u}] {level}: {message}");
    }
}
