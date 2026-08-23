# DungeonChessBattle.Replay

回放引擎：纯本地确定性重跑录制输入，零网络依赖。与 Server.Battle.Replay 录制端构成回放子系统。

## 职责范围

- 回放解码与重建：经 ReplayRecordCoder 解码快照，按头部与副本配置构建 BattleScene 与 BattleUnit；玩家单位按记录还原，敌人按副本生成顺序从 NextNetId 对齐 ID。
- 确定性驱动：固定逻辑步长推进；本地位移结算等价服务端 UnitPawn.Update（MoveResolver + 同源物理场景）；玩家输入按记录帧注入，施法仅注入服务端接受记录；AI 由战斗世界推进。
- 事件流输出：每帧返回领域事件，供回放 UI 事件消费。
- 状态直读：不注入移动桥与投影器，展示层经引擎直读 BattleScene 单位状态与玩家聚焦映射。
- 播放控制：播放/暂停/倍速；拖动经 SeekTo 从首帧确定性快进。

## 不负责

- 不依赖网络与 LES，不含回放下载与本地存储。
- 不实现 UI 场景与入口，展示节点、控制条与回放面板在 Godot 主工程 ReplayUI 命名空间。

## 依赖项

- Battle.Domain、Battle.Logic、GameConfig、Replay.Shared。
