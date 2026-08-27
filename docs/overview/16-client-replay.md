# DungeonChessBattle.Replay

回放引擎，与 Server.Battle.Replay 录制端构成回放子系统。职责边界见 `functional_boundary/16`。

## 机制

- 回放端与在线端共用同一 BattleScene：服务端装配投影器写 SyncVar、移动桥衔接实体；回放端不注入桥与投影器，位移由引擎本地结算，展示层经引擎直读 BattleScene 单位状态。
- 帧轴：回放第 N 帧对应战斗开始后第 N tick；输入记录帧为服务端绝对逻辑帧，注入条件为 记录.Frame - StartTick == 当前帧，战斗开始到首条输入之间的帧 AI 照常推进。
- 确定性：AI/伤害/移动均为纯函数无随机；重建顺序与 ID 对齐保证记录中的单位引用有效。
- 拖动：SeekTo 目标帧早于当前帧时重建战斗世界并从首帧快进。

## Godot 表现层装配

- 回放控制由 `MainScene` 直接承载：`ReplayCoordinator`（引擎编排与生命周期）与 `ReplayHud`（`replay_hud.tscn` 控制条，播放/暂停/倍速/拖动/退出，默认隐藏），不再有独立的回放表现场景。
- 回放复用 BattleInterface 的共享 3D 环境、相机与 `UnitShowManager`（无本地玩家，相机自由导航）；回放单位世界坐标与该环境同源，均落到世界原点平面。`UnitShowManager` 是单位视图唯一所有者，在线/回放经 Bind 切换数据源。
- 屏幕态仲裁由 `ScreenStateMachine` 统一：`ReplayCoordinator` 经 `ReplayStarted/ReplayFinished` 信号通知 `MainScene` 进入 `Replay` 态，隐藏前厅 `FrontUI` 与在线战斗 `GamePlayUI`，结束恢复。
- 展示数据源解耦：共享 `UnitShowManager.Bind(ReplayEngine)`，与在线（`RoomBattleClient`）共用 `IBattleViewSource` 契约；`UnitGameShow` 零改动。
- 事件反馈：复用在线 `UnitStateChangeInfo`，`ReplayCoordinator` 每帧把 `ReplayEngine.Step()` 的 `IBattleEvent` 流喂给它，弹出与在线共用的 `TookDamageInfo`/`BuffChangeInfo` 浮字；单位取数经注入的 `IBattleViewSource`（引擎），退出时解绑清空。
