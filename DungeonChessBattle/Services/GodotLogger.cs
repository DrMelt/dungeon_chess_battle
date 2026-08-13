using System;
using System.Collections.Concurrent;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Services;

/// <summary>
/// Godot ILoggerProvider，将日志输出到 GD.Print / GD.PrintErr。
/// </summary>
public sealed class GodotLoggerProvider : ILoggerProvider {
    /// <summary>按类别名缓存的日志器字典。</summary>
    private readonly ConcurrentDictionary<string, GodotLogger> _loggers = new();

    /// <summary>
    /// 按类别名创建或获取日志器实例。
    /// </summary>
    /// <param name="categoryName">日志类别名。</param>
    /// <returns>对应的日志器。</returns>
    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new GodotLogger(name));

    /// <summary>
    /// 释放提供者，清空所有缓存的日志器。
    /// </summary>
    public void Dispose() {
        _loggers.Clear();
    }
}

/// <summary>
/// Godot ILogger 实现。
/// </summary>
internal sealed class GodotLogger(string categoryName) : ILogger {
    /// <summary>日志类别名。</summary>
    private readonly string _categoryName = categoryName;

    /// <summary>
    /// Godot 日志不支持作用域，始终返回 null。
    /// </summary>
    /// <typeparam name="TState">状态类型。</typeparam>
    /// <param name="state">作用域状态。</param>
    /// <returns>始终为 null。</returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>
    /// 所有日志等级均被接受。
    /// </summary>
    /// <param name="logLevel">日志等级。</param>
    /// <returns>恒为 true。</returns>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>
    /// 按日志等级将消息输出到 Godot 控制台（Critical/Error 走 PrintErr，其余 Print）。
    /// 异常存在时输出完整 <c>ToString()</c>（含 Message 与全部 StackTrace），
    /// 避免 Godot 控制台对单行调用只展示摘要而丢失定位信息。
    /// </summary>
    /// <typeparam name="TState">状态类型。</typeparam>
    /// <param name="logLevel">日志等级。</param>
    /// <param name="eventId">事件 ID。</param>
    /// <param name="state">日志状态。</param>
    /// <param name="exception">异常信息。</param>
    /// <param name="formatter">消息格式化委托。</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel))
            return;

        string message = formatter(state, exception);
        string full = $"[{_categoryName}] {message}";
        if (exception != null)
            full += $"\n{exception}";

        switch (logLevel) {
            case LogLevel.Critical:
                GD.PrintErr("[Critical] " + full);
                break;
            case LogLevel.Error:
                GD.PrintErr("[Error] " + full);
                break;
            case LogLevel.Warning:
                GD.Print("[Warning] " + full);
                break;
            case LogLevel.Information:
                GD.Print("[Information] " + full);
                break;
            default:
                GD.Print(full);
                break;
        }
    }
}
