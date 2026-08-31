# DungeonChessBattle.Replay

回放引擎，与 Battle.Server.Replay 录制端、Replay.Server 服务侧、Replay.Client 获取端构成回放子系统。职责边界见 `functional_boundary/16`。

## 机制

- 回放端与在线端共用同一 BattleScene：服务端由状态同步器写 SyncVar；移动在 BattleScene.Tick 内统一结算，回放端不投影，展示层经引擎直读 BattleScene 单位状态。
- 帧轴：回放第 N 帧对应战斗开始后第 N tick；输入记录帧为服务端绝对逻辑帧，注入条件为 记录.Frame - StartTick == 当前帧，战斗开始到首条输入之间的帧 AI 照常推进。
- 确定性：AI/伤害/移动均为纯函数无随机；重建顺序与 ID 对齐保证记录中的单位引用有效。
- 拖动：SeekTo 目标帧早于当前帧时重建战斗世界并从首帧快进。

## Godot 表现层装配

- 表现层只管取数与呈现：`ReplayPanel` 经 `ServiceLocator.ReplayService`（Game 层浏览服务）拿已解码快照，`ReplayCoordinator.LoadReplay(snapshot)` 只构建引擎；过程状态在 `ReplayService`，面板 `_Process` 每帧读行视图渲染，下载进度、缓存命中与版本不符都表现为行状态。
- 回放控制由 `MainScene` 承载：`ReplayCoordinator`（引擎编排与生命周期）与 `ReplayHud`（控制条，默认隐藏）主控，不再有独立回放表现场景。
- 复用共享 3D 环境、相机与 `UnitShowManager`（单位视图唯一所有者，在线/回放经 `Bind` 切数据源，共用 `IBattleViewSource`，`UnitGameShow` 零改动）；屏幕态由 `ScreenStateMachine` 经 `ReplayStarted/ReplayFinished` 信号仲裁进入 `Replay` 态，隐藏前厅与在线战斗 UI。
- 事件反馈复用在线 `UnitStateChangeInfo`：`ReplayCoordinator` 每帧把 `ReplayEngine.Step()` 的 `IBattleEvent` 流喂给它，弹与在线共用的受击/治疗/Buff 浮字，退出解绑。
