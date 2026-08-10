namespace DungeonChessBattle.Services;

/// <summary>
/// 游戏服务器宿主的抽象接口，供 UI 层读取状态并启动/停止服务器。
/// 采用查询/命令式设计（无事件回调）：后台线程只更新加锁保护的内部状态，
/// 消费方在主线程轮询 <see cref="Status"/>，从根上避免跨线程触碰 Godot 节点。
/// 当前实现为独立子进程（<see cref="ServerProcessHost"/>），
/// 未来可替换为进程内托管或远程服务器实现而不影响上层调用。
/// </summary>
public interface IServerHost {
    /// <summary>当前运行状态。</summary>
    ServerHostStatus Status {
        get;
    }

    /// <summary>是否正在运行（<c>Status != <see cref="ServerHostStatus.Stopped"/></c>）。</summary>
    bool IsRunning {
        get;
    }

    /// <summary>当前监听端口；未就绪/未运行时为 0。</summary>
    int Port {
        get;
    }

    /// <summary>
    /// 最近一次启动失败或异常退出的原因描述；成功启动后清空。
    /// 用于 UI 展示失败原因（如可执行文件缺失、就绪超时）。
    /// </summary>
    string? LastError {
        get;
    }

    /// <summary>
    /// 启动服务器。
    /// </summary>
    /// <param name="port">大厅监听端口。</param>
    /// <param name="serverPassword">服务器访问密码；为空表示不启用。</param>
    void Start(int port, string? serverPassword = null);

    /// <summary>停止服务器。</summary>
    void Stop();
}
