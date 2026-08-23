# DungeonChessBattle.Server.Host

服务器可执行宿主，所属分组 Server。ASP.NET Core Kestrel + SignalR 装配层，游戏服务器进程入口，不含领域业务实现。职责边界见 `functional_boundary/14`。

## 入口装配

- `Program` 解析 `--port`（默认 10170）、读取环境变量 `DCB_SERVER_PASSWORD`；建日志工厂、`LesNetworkLogger.Install` 把 LES 日志接入统一日志体系（须早于任何 EntityManager 创建）。
- 装配 `ParentProcessWatcher`（从 `DCB_SERVER_PARENT_PID`），注册 Ctrl+C 优雅退出，随后阻塞运行。

## DI 组合根

`GameServerHost.Start` 构建 `WebApplication`：

- `ServerConfig.FromEnvironment` 产出装配配置，映射为模块配置切片：`LobbyServerConfig`（密码）、`BattleServerConfig`（连接密钥 + 房间端口池起点）。
- 注册 `InMemoryGameStateStore`、`InMemoryReplayStore`、`AddLobbyServer`、`AddBattleServer`、`AddSignalR`；绑定 `IBattleRoomManager` 到 `BattleRoomManager`。
- `MapHub<LobbyHub>("/lobby")` 承载大厅端点。
- `MapGet("/replay/{roomId}")` 回放下载端点：验证一次性凭证（`IReplayDownloadTicketStore.TryConsume`，绑定房间匹配）后经 `IReplayStore` 流式输出回放字节，不经 SignalR 通道。

## 后台循环

- 空房清理循环：`PeriodicTimer` 50ms 周期消费 `IBattleRoomManager.ProcessPendingRoomCleanups`，驱动房间线程投递的空房销毁。
- `Stop`：取消清理循环 → `StopAll`（停全部房间并归档回放）→ 停止 Kestrel。

## 进程看护

- `ParentProcessWatcher`：1 秒周期探测父进程启动时间。父进程消失或 PID 被系统复用（启动时间不一致）触发优雅退出；未配置父 PID 的独立运行模式不启用。

