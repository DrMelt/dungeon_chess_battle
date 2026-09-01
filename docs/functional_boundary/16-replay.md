# DungeonChessBattle.Replay

回放引擎：纯本地确定性重跑录制输入，零网络依赖。与 Battle.Server.Replay 录制端、Replay.Server 服务侧、Replay.Client 获取端共同构成回放子系统。

## 职责

- 回放解码与确定性重建：解码快照，按头部元数据与副本配置重建战斗场景；校验录制端内容修订号，不匹配拒绝重放。
- 确定性驱动：固定逻辑步长推进，记录条目还原为玩家命令后按帧注入同一个输入门面，AI 与位移由战斗世界计算。
- 事件流输出与状态直读，供回放 UI 消费。
- 播放控制：播放/暂停/倍速，支持从任意时间点确定性跳转。

## 边界外

- 不依赖网络与 LES，不含回放下载与本地存储：获取链路归 Replay.Client。
- 不实现 UI 场景与入口，展示与控制在 Godot 主工程。
- 不做字节流解码与内容版本门控，交进来的快照已解码。

## 依赖

- Battle.Shared、Battle.Logic、GameConfig、Replay.Shared。
