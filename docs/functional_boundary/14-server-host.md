# DungeonChessBattle.Server.Host

服务器可执行宿主，所属分组 Server。ASP.NET Core Kestrel + SignalR 装配层，游戏服务器进程入口。

## 职责范围

- 入口装配：解析 `--port` 与 `DCB_SERVER_PASSWORD`，建日志工厂、安装 LES 日志、启动宿主。
- DI 装配：模块配置切片、状态存储、广播端口、业务协调器注入，Hub 注册。
- 业务协调壳：`GameServer` 分派大厅请求、编排房间生命周期、处理连接断开清理。
- 进程看护：`ParentProcessWatcher` 检测父进程消失或 PID 复用触发优雅退出；Ctrl+C 优雅停机。

## 不负责

- 不实现业务逻辑：大厅、战斗、存储全部委托下层，Hub 端点面向 `ILobbyApplication` 契约。
- 不含子进程管理：拉起/停止由 Godot 端 `ServerProcessHost` 承担，本侧只响应父进程看护契约。


## 与周边协作

- 上游：Godot 客户端 Debug 构建自动构建；`ServerProcessHost` 拉起子进程。
- 下游：Server.Lobby、Server.Battle、Server.StateStore 与共享层契约。
