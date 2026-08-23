# DungeonChessBattle.Client.Replay

客户端本地回放引擎，所属分组 Client。职责边界见 `functional_boundary/16`。

## 机制

- 回放端与在线端共用同一 BattleScene：服务端装配投影器写 SyncVar、移动桥衔接实体；回放端不注入桥，位移由引擎本地结算，状态投影到 ReplayUnitView。
- 帧轴：回放第 N 帧对应战斗开始后第 N tick；输入记录帧为服务端绝对逻辑帧，注入条件为 记录.Frame - StartTick == 当前帧，战斗开始到首条输入之间的帧 AI 照常推进。
- 确定性：AI/伤害/移动均为纯函数无随机；重建顺序与 ID 对齐保证记录中的单位引用有效。
- 拖动：SeekTo 目标帧早于当前帧时重建战斗世界并从首帧快进。
