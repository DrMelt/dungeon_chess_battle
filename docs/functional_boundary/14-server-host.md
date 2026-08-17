# DungeonChessBattle.Server.Host

服务器可执行宿主，所属分组 Server。ASP.NET Core Kestrel + SignalR 装配层，游戏服务器进程入口，不含领域业务实现。

## 职责范围

- 入口装配：解析 `--port` 与 `DCB_SERVER_PASSWORD`，建日志工厂、安装 LES 日志、启动宿主。
- DI 组合根：模块配置切片、状态存储、广播端口、`IRoomServerManager` 绑定 Server.Battle 实现、协调器与 Hub 注册。
- 空房间清理后台循环，经 `IRoomServerManager.ProcessPendingRoomCleanups`。
- 进程看护：`ParentProcessWatcher` 检测父进程消失或 PID 复用触发优雅退出；Ctrl+C 优雅停机。

## 不负责

- 不实现业务逻辑：大厅、战斗、存储全部委托下层。
- 不含子进程管理：拉起/停止由 Godot 端 `ServerProcessHost` 承担，本侧只响应父进程看护契约。
- 不定义服务端抽象契约：`IRoomServerManager`、`ILobbyBroadcaster` 在 Server.Abstractions。

## 依赖项

- Server.Lobby、Server.Battle、Server.Abstractions、Server.StateStore、Server.StateStore.Abstractions 与共享层契约（Protocol、Entities）。
