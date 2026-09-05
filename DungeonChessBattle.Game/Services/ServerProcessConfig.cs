using System;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 服务器子进程启动配置。所有字段均有合理默认值，可在创建
/// <see cref="ServerProcessHost"/> 时通过构造参数覆盖。
/// </summary>
public sealed record ServerProcessConfig {
    /// <summary>子进程工作目录；为空时使用可执行文件所在目录。</summary>
    public string? WorkingDirectory {
        get; init;
    }

    /// <summary>就绪探测超时。</summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>就绪探测轮询间隔。</summary>
    public TimeSpan ReadyPollInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>mods 根目录绝对路径，经环境变量注入子进程；为空时不传，服务器按纯内置内容启动。</summary>
    public string? ModDirectory {
        get; init;
    }
}
