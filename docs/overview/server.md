# 服务端装配域内部机制

覆盖 `Server.Host` 与 `Server.Abstractions`。跨进程握手见 `flow/connection-reconnect`；模块边界见 `functional_boundary/14`、`15`。

## 入口与组合根

- `Program` 解析 `--port`（默认 10170）与环境变量密码，建日志工厂，`LesNetworkLogger.Install` 把 LES 日志接入统一日志体系——须早于任何 EntityManager 创建，否则框架日志丢失转接。
- `GameServerHost.Start` 构建 `WebApplication`。`ServerConfig.FromEnvironment` 产出装配配置后映射为模块配置切片：`LobbyServerConfig` 只拿密码、`BattleServerConfig` 只拿连接密钥与房间端口池起点——模块不读全量环境。
- 注册内存存储、回放存储、`IPlayerIdentityResolver`（适配器包 `IGameStateStore`）、`AddLobbyServer`、`AddBattleServer`、`AddReplayServer`，绑定 `IBattleRoomManager` 到 `BattleRoomManager`。
- `MapHub<LobbyHub>("/lobby")` 只承载大厅端点；回放有自己的 HTTP 端点，与 SignalR 无关。
- 构建完成后立刻解析 `GameServer`，让大厅 DI 图缺件在启动时炸出，而非等到首个请求。

## 后台循环与退出

- 空房清理循环：`PeriodicTimer` 50ms 周期消费 `ProcessPendingRoomCleanups`，驱动房间线程投递的空房销毁。清理权力在宿主，房间只投递不自行销毁。
- `Stop` 顺序：取消清理循环 → `StopAll`（停全部房间并归档回放）→ 停止 Kestrel。归档发生在 Kestrel 停止之前，否则最后一段归档写不进去。
- `ParentProcessWatcher`：1 秒周期探测父进程启动时间，父进程消失或 PID 被系统复用（启动时间不一致）触发优雅退出；未配置父 PID 的独立运行模式不启用。

## 契约层四类端口

| 端口 | 实现 | 消费 |
|---|---|---|
| `IBattleRoomManager` 房间生命周期原语 | Battle.Server `BattleRoomManager` | Lobby.Server 协调、Host 清理循环与关服 |
| `ILobbyBroadcaster` 房间内广播 | Lobby.Server `SignalRBroadcaster` | Lobby.Server 业务 |
| `IPlayerIdentityResolver` 凭证 → 玩家记录主键 | Server.DataStore `PlayerIdentityResolver` | Replay.Server 端点 |
| `IReplayStore` 归档读写与按主键检索 | Server.DataStore `InMemoryReplayStore` | 写 Battle.Server、读 Replay.Server |

- 入参与返回值限原生类型、字符串与纯原语 DTO；零依赖契约库，供服务端各库与宿主共享，实现与调用方互不感知。
- 只映射不签发：凭证由大厅登录换发、随连接作废；解析不出主键就等于「这串凭证不代表任何人」，调用方据此拒绝。
- 摘要模型不在本层：归档字节流自身的元数据块是唯一真相，本端口只认字节与主键。回放记录格式契约在 `Replay.Shared`，HTTP 契约在 `Replay.Protocol`。
- 不覆盖大厅自身鉴权：Hub 上的业务身份仍是登录时自报的玩家名，对外暴露前需先加固登录。
