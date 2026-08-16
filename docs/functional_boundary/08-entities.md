# DungeonChessBattle.Entities

LES 网络实体层，所属分组 Shared。服务端与客户端共用的实体类型、类型注册表与协议载荷。

## 职责范围

- 网络实体：`BattleRoomEntity`、`PlayerRoomEntity`、`UnitPawn`、`UnitController`。
- 类型注册表与自定义 SyncVar 字段类型注册，LES 日志转接。
- 协议载荷：可靠请求与同步数据载荷。
- 移动输入流注入与可靠请求订阅。

## 不负责

- 不实现战斗结算与规则，权威在 BattleEngine。
- 不映射为 UI 事件，翻译由 Client.Battle 实体映射层承担。
- 不含连接管理与会话逻辑。

## 依赖项

- Protocol、Battle.Domain。
