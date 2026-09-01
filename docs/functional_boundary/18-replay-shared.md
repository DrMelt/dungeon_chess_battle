# DungeonChessBattle.Replay.Shared

回放归档格式契约库，所属分组 Shared。服务端导出与客户端下载解析共用的容器格式、数据模型与编解码，独立于网络契约层。

## 职责

- 自包含分块容器 `ReplayArchive`：容器头尾、逐块长度与校验和、已知块唯一性断言、Deflate 编解码、未知块跳过；版本门控（`FormatVersion`）内建于解码路径。
- 回放内容模型：元数据（帧轴、两项修订号、玩家表）、单位初始态、按玩家分轨的方向意图段、施法与聚焦条目。
- 只读元数据入口 `TryReadMeta`：让客户端枚举本地缓存条目而不必整档解码。
- 玩家命令与归档条目的双向映射唯一权威：移动段的折叠判据、账本骨架与轨道成型也在此，录制端只持时间轴与账本。
- 块类型表与预留位（关键帧）；内容修订号与逻辑修订号由重放端校验，本层不做内容门控。

## 边界外

- 不含录制逻辑，录制在 Battle.Server.Replay。
- 不含回放重跑逻辑，重跑在 Replay 引擎。
- 不含归档的查询、凭证与下载，归 Replay.Server 与 Replay.Client。
- 不含回放归档存储契约，归 Server.Abstractions；不持有摘要模型，摘要即元数据块。
- 不依赖网络栈，格式独立于传输通道。

## 依赖

- MessagePack、`System.IO.Compression`；Battle.Shared（取 `PlayerCommand` 输入形状与 `UnitId` 强类型，供条目双向映射）。两项修订号不经本层：`GameConfigDB.DataRevision` 与 `BattleLogicRevision.Value` 由录制端读进元数据，本层只搬运字段不解释其值。
