# DungeonChessBattle.Replay.Shared

回放记录格式契约，服务端导出与客户端下载解析共用。职责边界见 `functional_boundary/18`。

## 机制

- 模型以 MessagePack 显式 Key 索引标注，字段重命名与顺序调整不破坏兼容。
- `ReplayRecordCoder` 为编解码唯一权威，解码时校验格式版本，版本不匹配抛异常。
- 编码端：Server.Battle `BattleReplayRecorder` 收集输入条目，`BattleRoomManager` 归档时 Encode 为字节流存入 `IReplayStore`。
- 解码端：客户端 `ReplayCoordinator` 经 HTTP 下载字节流后 Decode，构建 `ReplayEngine` 本地重跑。
