# DungeonChessBattle.Server.Host

服务器可执行宿主。ASP.NET Core Kestrel + SignalR 装配层，游戏服务器进程入口，不含领域业务实现。

## 职责

- 入口装配：解析命令行与环境变量并启动宿主。
- 依赖装配：模块配置、数据存储、广播端口与房间管理绑定、协调器与 Hub 注册。
- 进程看护：父进程消失或 PID 复用触发优雅退出。

## 边界外

- 不实现业务逻辑：大厅、战斗、回放、存储全部委托下层；回放下载端点只调用回放侧的映射扩展。
- 不含子进程管理：拉起/停止由 Godot 端承担，本侧只响应父进程看护契约。
- 不定义服务端领域契约：房间生命周期契约在 Battle.Server.Shared，存储契约在 Server.DataStore.Shared。

## 依赖

- Lobby.Server、Battle.Server、Replay.Server、Battle.Server.Shared、Server.DataStore、Server.DataStore.Shared 与共享层契约（Battle.Entities）。
