using System;
using System.Collections.Concurrent;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Services;

/// <summary>
/// Godot ILoggerProvider，将日志输出到 GD.Print / GD.PrintErr。
/// </summary>
public sealed class GodotLoggerProvider : ILoggerProvider {
    private readonly ConcurrentDictionary<string, GodotLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new GodotLogger(name));

    public void Dispose() {
        _loggers.Clear();
    }
}

/// <summary>
/// Godot ILogger 实现。
/// </summary>
internal sealed class GodotLogger(string categoryName) : ILogger {
    private readonly string _categoryName = categoryName;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel))
            return;

        string message = formatter(state, exception);
        string full = $"[{_categoryName}] {message}";

        switch (logLevel) {
            case LogLevel.Error:
            case LogLevel.Critical:
                GD.PrintErr(full);
                break;
            case LogLevel.Warning:
                GD.PushWarning(full);
                break;
            default:
                GD.Print(full);
                break;
        }
    }
}
