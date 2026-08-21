using System;
using UnityEngine;

namespace CloudX
{
/// <summary>
/// Internal logger for CloudX SDK with configurable log level and automatic tag prefixing.
/// </summary>
internal class Logger
{
    private readonly string _tag;

    /// <summary>
    /// The minimum log level. Messages below this level are filtered out.
    /// Defaults to <see cref="CloudXLogLevel.Error"/>.
    /// </summary>
    public CloudXLogLevel CurrentLogLevel { get; set; } = CloudXLogLevel.Error;

    /// <summary>
    /// Creates a new logger instance with the specified tag.
    /// </summary>
    /// <param name="tag">Tag to prepend to all log messages (without brackets).</param>
    public Logger(string tag)
    {
        _tag = tag;
    }

    private string FormatMessage(Func<string> messageSupplier) => $"[{_tag}] {messageSupplier()}";

    /// <summary>
    /// Logs an info message if current log level permits.
    /// </summary>
    public void LogInfo(Func<string> messageSupplier)
    {
        if (CurrentLogLevel <= CloudXLogLevel.Info)
        {
            Debug.Log(FormatMessage(messageSupplier));
        }
    }

    /// <summary>
    /// Logs an error message if current log level permits.
    /// </summary>
    public void LogError(Func<string> messageSupplier, Exception error = null)
    {
        if (CurrentLogLevel <= CloudXLogLevel.Error)
        {
            Debug.LogError(FormatMessage(messageSupplier));
            if (error != null) Debug.LogException(error);
        }
    }

    /// <summary>
    /// Logs a warning message if current log level permits.
    /// </summary>
    public void LogWarning(Func<string> messageSupplier)
    {
        if (CurrentLogLevel <= CloudXLogLevel.Warn)
        {
            Debug.LogWarning(FormatMessage(messageSupplier));
        }
    }

    /// <summary>
    /// Logs a debug message if current log level permits.
    /// </summary>
    public void LogDebug(Func<string> messageSupplier)
    {
        if (CurrentLogLevel <= CloudXLogLevel.Debug)
        {
            Debug.Log(FormatMessage(messageSupplier));
        }
    }

    /// <summary>
    /// Logs a verbose message if current log level permits.
    /// </summary>
    public void LogVerbose(Func<string> messageSupplier)
    {
        if (CurrentLogLevel <= CloudXLogLevel.Verbose)
        {
            Debug.Log(FormatMessage(messageSupplier));
        }
    }
}
}
