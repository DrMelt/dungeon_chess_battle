# DungeonChessBattle.Server.Host

服务器可执行宿主，所属分组 Server。ASP.NET Core Kestrel + SignalR 装配层，游戏服务器进程入口，不含领域业务实现。

## 职责

- 入口装配：解析端口与密码参数并启动宿主。
- DI 组合根：模块配置、数据存储、广播端口与房间管理绑定、协调器与 Hub 注册。
- 回放下载端点：验证一次性凭证后输出回放字节。
- 进程看护：父进程消失或 PID 复用触发优雅退出。

## 边界外

- 不实现业务逻辑：大厅、战斗、存储全部委托下层。
- 不含子进程管理：拉起/停止由 Godot 端承担，本侧只响应父进程看护契约。
- 不定义服务端抽象契约：房间管理与广播契约在 Server.Abstractions。

## 依赖

- Server.Lobby、Server.Battle、Server.Abstractions、Server.DataStore、Server.DataStore.Shared 与共享层契约（Protocol、Entities）。
