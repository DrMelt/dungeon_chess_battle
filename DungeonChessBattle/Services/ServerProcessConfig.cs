using System;

namespace DungeonChessBattle.Services;

/// <summary>
/// 服务器子进程启动配置。所有字段均有合理默认值，可在创建
/// <see cref="ServerProcessHost"/> 时通过构造参数覆盖。
/// 典型场景无需手动配置：可执行文件路径由宿主按约定自动解析，
/// 亦可通过环境变量 <c>DCB_SERVER_EXE</c> / <c>DCB_SERVER_CONFIG</c> 覆盖。
/// </summary>
public sealed record ServerProcessConfig {
    /// <summary>
    /// 服务器可执行文件（.exe 或 .dll）绝对路径。
    /// 为空时由宿主按约定自动解析（相对 Godot 工程目录下 Server 工程输出目录）。
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>子进程工作目录；为空时使用可执行文件所在目录。</summary>
    public string? WorkingDirectory {
        get; init;
    }

    /// <summary>就绪探测超时。</summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>就绪探测轮询间隔。</summary>
    public TimeSpan ReadyPollInterval { get; init; } = TimeSpan.FromMilliseconds(200);
}
