namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 服务器子进程跨进程契约的环境变量名。
/// 客户端 ServerProcessHost 写入，服务器端入口与父进程看护读取，两端共享单一来源。
/// </summary>
public static class ServerProcessEnv {
    /// <summary>服务器访问密码，经环境变量传递避免暴露在进程命令行。</summary>
    public const string Password = "DCB_SERVER_PASSWORD";

    /// <summary>父进程 PID，服务器端 ParentProcessWatcher 读取，客户端消失时防孤儿进程。</summary>
    public const string ParentPid = "DCB_SERVER_PARENT_PID";

    /// <summary>mods 根目录绝对路径，客户端与服务器子进程共享同一份启用内容。</summary>
    public const string ModDir = "DCB_SERVER_MOD_DIR";
}
