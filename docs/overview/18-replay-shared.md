# DungeonChessBattle.Replay.Shared

回放记录格式契约，服务端导出与客户端下载解析共用。职责边界见 `functional_boundary/18`。

## 机制

- 模型以 MessagePack 显式 Key 索引标注，字段重命名与顺序调整不破坏兼容。
- `ReplayCommands` 是玩家命令与记录条目的双向映射唯一权威：载荷拆分只有一份实现，录制端与重放端无从各写一遍。键的换算不在其中——命令持网络 ID，条目持玩家表序号，两端各自持有反查表。
- `ReplayRecordCoder` 为编解码唯一权威，解码时校验格式版本，版本不匹配抛异常。
- `Header.DataVersion` 为录制端 `GameConfigDB.DataRevision`，客户端构建 `ReplayEngine` 时校验一致，不一致拒绝重放，杜绝数据演化导致的旧回放静默漂移。
- 编码端：Battle.Server `BattleReplayRecorder` 收下玩家命令，经 `ReplayCommands` 落成条目，`BattleRoomManager` 归档时 Encode 为字节流存入归档存储端口（见 `overview/15`）。
- 解码端：客户端 `Replay.Client` 只取归档字节流；Game 层 `ReplayService` 解码并内容版本门控后交快照过来，`ReplayEngine` 只管按帧重跑。
