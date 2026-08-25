# DungeonChessBattle.Protocol

网络契约与网络约定常量的共享库，所属分组 Shared。服务端与客户端两侧共用，消除端口、协议帧与字段长度魔法值。

## 职责

- 网络协议常量：字段长度约束 `EntityConstants` 与网络默认值 `NetworkDefaults`，两端共用。
- 客户端连接最小契约 `IClientConnection`，供门面统一驱动。
- 跨进程启动契约 `ServerProcessEnv`：服务端子进程环境变量约定。

## 边界外

- 大厅网络契约（Hub 方法名与大厅 DTO）归 Lobby.Protocol，见 `functional_boundary/19`。
- 不含业务实现与运行时逻辑，纯 .NET 类库，无第三方运行时依赖。
- 不含业务默认值与配置域常量，默认副本键归 GameConfig。
- 不含回放记录格式与编解码，归 Replay.Shared。
- 不受理连接建立与重连，连接事件由大厅与房间两端各自实现。
- 战斗周期消息结构由 LES 实体类型系统表述，不定义于本层。

## 依赖

- 无：纯 .NET 类库。
