# DungeonChessBattle.Replay.Shared

回放记录格式契约库，所属分组 Shared。服务端导出与客户端下载解析共用的回放数据模型与编解码，独立于网络契约层。

## 职责范围

- 回放记录模型：头部元数据、玩家初始状态、移动/施法/聚焦输入条目与只读快照，MessagePack 显式索引标注。
- 编解码：`ReplayRecordCoder` 编码/解码快照为字节流，格式版本门控校验，服务端与客户端共用单一权威实现。
- 格式版本：`ReplayFormatVersion` 常量，数据模型或编码变化时递增。

## 不负责

- 不含录制逻辑，录制在 Server.Battle.Replay。
- 不含回放重跑逻辑，重跑在 Replay 引擎。
- 不依赖网络栈，格式独立于传输通道。

## 依赖项

- MessagePack。
