# DungeonChessBattle.Replay

回放引擎，与 Battle.Server.Replay 录制端、Replay.Server 服务侧、Replay.Client 获取端构成回放子系统。职责边界见 `functional_boundary/16`。

## 机制

- 回放端与在线端共用同一 BattleScene：服务端由状态同步器写 SyncVar；移动在 BattleScene.Tick 内统一结算，回放端不投影，展示层经引擎直读 BattleScene 单位状态。
- 回放第 N 帧对应战斗开始后第 N tick；条目帧为服务端绝对逻辑帧，注入条件为 条目.Frame - StartTick == 当前帧，战斗开始到首条输入之间的帧 AI 照常推进。
- 每帧顺序与服务端 `BattleLoop` 钩子同形：门面 `PrepareTick`（AI 决策 → 在架施法重试）→ 注入本帧条目 → `Tick`。条目经 `ReplayCommands` 还原为玩家命令，交与在线同一个门面提交；注入内部按施法 → 移动 → 聚焦，与服务端落点同序，移动与施法都只登记意图、裁定在 `Tick` 内单点完成，故注入先后不影响结果。施法排队后的落地时刻一并复现；`Accepted=false` 的条目直接跳过，以服务端结论为准，接管后仍被门内规则拒绝的（如聚焦目标已死亡）同样落空。
- 移动轨道逐玩家持一个游标：方向意图段覆盖本帧即重投该段方向，段尽或帧未覆盖则不投——与在线"输入源逐 tick 重投、`Tick` 末作废"完全同构，收拢只是存储侧的事。
- 单位重建照归档 `UnitInit` 表：ID、阵营与出生点取记录值，属性按配置键取当前配置，玩家单位不挂 AI——不再从"实体 ID 连续分配"这类运行期前提推演敌人。
- 时间轴取元数据 `EndTick`（战斗结束帧），不由最后一条输入倒推，收尾段可看。
- 确定性：AI/伤害/移动均为纯函数无随机；构造期双重门控内容修订号与逻辑修订号。
- 拖动：SeekTo 目标帧早于当前帧时重建战斗世界并从首帧快进，重建同时清空预输入缓冲与各移动游标——在架意图持旧单位引用。

## Godot 表现层装配

- 表现层只管取数与呈现：`ReplayPanel` 经 `ServiceLocator.ReplayService`（Game 层浏览服务）拿已解码记录，`ReplayCoordinator.LoadReplay(recording)` 只构建引擎；过程状态在 `ReplayService`，面板 `_Process` 每帧读行视图渲染，下载进度、缓存命中与版本不符都表现为行状态。
- 回放控制由 `MainScene` 承载：`ReplayCoordinator`（引擎编排与生命周期）与 `ReplayHud`（控制条，默认隐藏）主控，不再有独立回放表现场景。
- 复用共享 3D 环境、相机与 `UnitShowManager`（单位视图唯一所有者，在线/回放经 `Bind` 切数据源，共用 `IBattleViewSource`，`UnitGameShow` 零改动）；屏幕态由 `ScreenStateMachine` 经 `ReplayStarted/ReplayFinished` 信号仲裁进入 `Replay` 态，隐藏前厅与在线战斗 UI。
- 事件反馈复用在线 `UnitStateChangeInfo`：`ReplayCoordinator` 每帧把 `ReplayEngine.Step()` 的 `IBattleEvent` 流喂给它，弹与在线共用的受击/治疗/Buff 浮字，退出解绑。
