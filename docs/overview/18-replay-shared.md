# DungeonChessBattle.Replay.Shared

回放归档格式契约，服务端导出与客户端下载解析共用。职责边界见 `functional_boundary/18`。

## 容器

归档是一份自包含字节流，`ReplayArchive` 是它的唯一读写口：

```
"DCBR"(4) | u16 FormatVersion | u16 MinorVersion
chunk*  : u16 Type | u8 Codec | u32 StoredLen | u32 RawLen | u32 Crc32 | payload
"DCBR"(4)
```

- 首块恒为不压缩的 `Meta`：只读前缀即可拿摘要，`TryReadMeta` 因此能在不整档解码的前提下列出本地缓存。入参恒为自归档第 0 字节起的前缀，不是某一段块体——前缀不足时回 `NeedMoreData` 并给出绝对长度 `RequiredBytes`，在同一缓冲里续读两轮即够。
- 块负载一律 MessagePack 显式 Key 模型，字段重命名与顺序调整不破坏兼容；重复字符串由块级 Deflate 吃掉，不设字符串表。
- 校验和算在存储字节上，覆盖压缩与传输两段；尾部魔数是完整性判据。截断、位翻转、外部文件与版本不认在解码前就分家，`Malformed` 不再等于"抛了个异常"。
- 未知块跳过：新增块只升 `MinorVersion`，旧读侧照常重放其余部分；改既有块语义才升 `FormatVersion`（当前 6，v5 及更早的单包数组不再可读）。
- 已知块每类至多一个：`Meta`/`UnitInit`/`MoveTrack`/`Cast`/`Focus` 由写侧保证单块，读侧对重复块判 `Malformed`——累加会造出同 ID 双单位或整条轨道被后到的吃掉。未知类型不受该断言约束，多块语义由未来的块自己定义。
- `Keyframe` 块只占号，写侧待战斗世界状态序列化落地。

## 机制

- 内容分五块：`Meta`（房间、帧轴 `StartTick`/`EndTick`、两项修订号、玩家表）、`UnitInit`（全部单位的 NetId、配置键、阵营、出生点）、`MoveTrack`（按玩家分轨的方向意图段）、`Cast`、`Focus`。玩家身份不在 `UnitInit`：由玩家表的 NetId 判定，一份事实不留两个落点。
- 世界重建照 `UnitInit`，实体 ID 不再由"准备期玩家单位连续建完"这类前提推演；引用了表外单位的条目在门内解析落空，与在线端一样按无效目标处理，不另设报错通道。
- 移动分轨合并只改存储不改语义：帧连续且方向位相同即续段，读侧展开为逐帧重投，`Tick` 末作废的意图契约不变。方向分量 bit-exact，不做量化。
- 时间轴取 `EndTick`，不由最后一条输入倒推：战斗打完之后的收尾段也在进度条上。
- `ReplayCommands` 是玩家命令与归档条目的双向映射唯一权威，段折叠判据、账本骨架与轨道成型同在此——录制端只持时间轴与三本账，两项修订号由调用方读好供给。玩家数上限 `ReplayMoveTrack.MaxPlayers`（256）只定这一处，录制、成型与重建各自守它。命令持 `UnitId`，条目持玩家表序号与 `ushort` 目标 ID，ID 升降级只在这组映射里发生。
- 双修订号：`DataVersion` 是录制端 `GameConfigDB.DataRevision`（配置与布局），`LogicVersion` 是录制端 `BattleLogicRevision.Value`（结算时序与事件顺序）。两者由重放端校验，任一不符拒绝重放；`ReplayArchive` 不管内容门控，只保证容器合规范。
- 编码端：Battle.Server `BattleReplayRecorder` 产出 `ReplayRecording`，`BattleRoomManager` 归档时 `Encode` 为字节流。解码端：Game 层 `ReplayService` 解码并门控后交 `ReplayRecording`，`ReplayEngine` 只管按帧重跑。
