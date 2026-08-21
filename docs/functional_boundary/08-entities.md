# DungeonChessBattle.Entities

LES 网络实体层，所属分组 Shared。服务端与客户端共用的实体类型、类型注册表与协议载荷。

## 职责范围

- 网络实体：`BattleRoomEntity`、`UnitPawn`、`UnitController`；`BattleRoomEntity` 实现 `IBattleRoom` 房间级战斗状态权威载体契约，不承载事件日志，`UnitPawn` 实现 `IBattleUnit` 并承载服务端权威战斗状态 `UnitCombatState`，不参与网络同步。
- 类型注册表与自定义 SyncVar 字段类型注册，LES 日志转接。
- 协议载荷：可靠请求、同步数据载荷、战斗事件日志编解码 `SyncBattleEvent`/`BattleEventCoder` 与可靠消息帧编解码 `ReliableMessageFrame`/`ReliableBattleEventLog`。
- 移动输入流注入与可靠请求订阅。
- 服务器 tick 换算工具 `SyncTickHelper`：倒计时同步统一为截止 tick，服务端载体与客户端 UI 共用推算。

## 不负责

- 不实现战斗结算与规则，权威在 BattleScene。
- 不映射为 UI 事件，翻译由 Client.Battle 实体映射层承担。
- 不含连接管理与会话逻辑。

## 依赖项

- Protocol、Battle.Domain。
