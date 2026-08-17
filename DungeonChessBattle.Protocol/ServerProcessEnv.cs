namespace DungeonChessBattle.Protocol;

/// <summary>
/// 服务器子进程跨进程契约的环境变量名。
/// 客户端 ServerProcessHost 写入，服务器端入口与父进程看护读取，两端共享单一来源。
/// </summary>
public static class ServerProcessEnv {
    /// <summary>服务器访问密码，经环境变量传递避免暴露在进程命令行。</summary>
    public const string Password = "DCB_SERVER_PASSWORD";

    /// <summary>父进程 PID，服务器端 ParentProcessWatcher 读取，客户端消失时防孤儿进程。</summary>
    public const string ParentPid = "DCB_SERVER_PARENT_PID";

    /// <summary>服务器可执行文件显式路径覆盖。</summary>
    public const string ExecutablePath = "DCB_SERVER_EXE";

    /// <summary>服务器构建配置，Debug / Release。</summary>
    public const string BuildConfig = "DCB_SERVER_CONFIG";
}
