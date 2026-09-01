# DungeonChessBattle.Replay.Shared

回放记录格式契约库，所属分组 Shared。服务端导出与客户端下载解析共用的回放数据模型与编解码，独立于网络契约层。

## 职责

- 回放记录模型：头部元数据（含录制端内容数据修订号 `DataVersion`）、玩家初始状态与移动/施法/聚焦输入条目。
- 玩家命令与记录条目的双向映射唯一权威，使录制端与重放端共用同一份载荷拆分口径。
- 统一编解码唯一权威：服务端与客户端共用；只读记录头部入口让客户端枚举本地缓存条目元数据而不必整包解码。
- 格式版本常量，数据模型或编码变化时递增；内容修订号 `DataVersion` 门控数据演化一致性（由重放端校验）。

## 边界外

- 不含录制逻辑，录制在 Battle.Server.Replay。
- 不含回放重跑逻辑，重跑在 Replay 引擎。
- 不含归档的查询、凭证与下载，归 Replay.Server 与 Replay.Client。
- 不含回放归档存储契约与摘要模型，归 Server.Abstractions。
- 不依赖网络栈，格式独立于传输通道。

## 依赖

- MessagePack；Battle.Shared（只取 `PlayerCommand` 输入形状，用于条目映射）。
