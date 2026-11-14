namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 游戏服务器宿主的运行状态。
/// 采用三态以区分"进程已拉起但尚未就绪"（<see cref="Starting"/>）与
/// "就绪可连接"（<see cref="Running"/>），避免仅用布尔值丢失中间态信息。
/// </summary>
public enum ServerHostStatus {
    /// <summary>未运行。</summary>
    Stopped = 0,

    /// <summary>进程已启动，就绪探测中。</summary>
    Starting = 1,

    /// <summary>就绪，端口可连接。</summary>
    Running = 2,
}
