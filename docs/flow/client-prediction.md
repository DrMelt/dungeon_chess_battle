# 在线客户端预测

状态：实施方案未定。本文只记录 LES 框架在客户端的行为、官方示例的做法与本项目当前时序，不提供职责落点与影响面。

LES 自身时序与实体钩子可达性见 [lite-entity-system-update](../libraries/lite-entity-system-update.md)，权威状态下行链见 [battle-state-sync](battle-state-sync.md)。本文只写 LES 在在线客户端的实际行为、可复现性判据与已知缺陷。

## 不变量

三条，任一条被破坏都会产生可见缺陷。

| 编号 | 约束 | 被破坏时的现象 |
|---|---|---|
| I1 | 客户端只写本地控制实体的 predicted 字段，他人 `Value` 一律不写 | 他人 `Value` 由下行写入且是当前显示读数的唯一来源，写脏后权威值被本端猜测覆盖，静止单位漂移、移动单位每次下行回抽 |
| I2 | 预测步进只发生在 `entity.Update()` 内，只用 `EntityManager.DeltaTimeF`，只依赖自身输入与静态几何 | 步进不进回滚重放，预测窗口锁死一个 tick；步长与权威不同源，轨迹偏差每个下行状态到达即被纠正一次 |
| I3 | `InterpolatedValue` 只在 `ClientEntityManager.Update()` 返回之后读 | 读到上一帧的插值进度与状态切换前的端点，与渲染帧率产生拍频 |

## 当前时序与缺陷

一个渲染帧内的实际次序：

| 次序 | 位置 | 内容 |
|---|---|---|
| 1 | `BattleCoordinator._Process` | `process_priority = -1` 使其先于网络驱动：采集移动输入并写 pending（`UnitController.SubmitInput` → `ModifyPendingInput`） |
| 2 | `GameClientDriver._Process` | 驱动 `ClientService.Update` |
| 3 | `NetworkClientBase.Update` | `PollEvents` → `OnNetworkReceiveInternal` → `ClientEntityManager.Deserialize`，下行状态入插值缓冲 |
| 4a | `EntityManager.Update` 开头 | 单例 `ClientBattleLoop.VisualUpdate`：`UnitPawn.SyncInto` 读全体载体的 `Value` 覆写领域 `BattleUnit`，本帧下行 diff 尚未应用 |
| 4b | `EntityManager.Update` 累加器内 | 单例 `ClientBattleLoop.Update`/`LateUpdate` 均为空实现；`OnLogicTick` 存输入头并跑本地控制实体的 `entity.Update`，`UnitPawn` 未覆写该钩子 |
| 4c | `ClientEntityManager.Update` 后段 | 补发输入 → `GoToNextState` 回滚重放 → 下行 diff 写进实体字段 → 推进 `_remoteLerpFactor` |
| 4d | `ClientEntityManager.Update` 末尾 | 逐实体 `VisualUpdate`；`UnitPawn` 未标 `UpdateOnClient`，`AliveEntities` 内只有本地控制实体 |
| 5 | `UnitGameShow._Process` | 直读 `BattleUnit.Position`、`Direction` 写 transform，无二次平滑 |

在线端当前不存在预测模拟：客户端不跑输入门面的 `PrepareTick` 与整场景 `Tick`，同步通道只有投影与回填两个方向、无本地回写，本地 `BattleScene` 只作展示回填容器，主控与他人的位移一律由服务端下行的 `Value` 决定。下行节奏是 `sendRate = ServerSendRate.EqualToFPS`，每 tick 一个状态，128 Hz 高于常见渲染帧率，直读 `Value` 不产生可见阶跃。

| 编号 | 缺陷 | 现象与触发条件 | 违反 |
|---|---|---|---|
| D5 | 主控单位无本地步进，位移全等下行 | 操作响应至少滞后 RTT/2 加缓冲水位，本端没有可被纠正的预测位移 | — |
| D9 | `UnitPawn` 全项目不带 `SyncVarFlags`，`SyncFlags.Interpolated` 未标注 | `EntityClassData` 的 flags 只取自字段或所在类上的该特性，默认 `None`；未标注则框架不写 `_interpValue`，`InterpolatedValue` 对远端实体退化，展示只能读 `Value` | — |
| D10 | 回填落在 `EntityManager.Update` 开头，早于 4c 的下行写回 | 展示读数恒比本端已收到的权威值旧一个渲染帧 | I3 |
| D11 | 移动输入无时效：服务端输入队列排空的 tick 仍转发 `CurrentInput` 末值 | 连接在而客户端卡发送时单位按末值持续位移。领域侧已改为按帧作废的输入，转发一停即静止，但 `PawnLogic.Update` 每 tick 无条件转发末值，卡发送未被覆盖——时效缺在转发层，不在字段生命周期 | — |

已关闭：D1（本地结算结果回写他人 `Value`，回写通道已删除）、D2（回填对插值进度的依赖，改读 `Value` 后不存在，代价转入 D9）、D3（客户端 `Tick` 用渲染帧 dt）、D4（客户端重跑敌方 AI 与整场景结算）、D7（房间线程 `Thread.Sleep(1)` 控制轮询节奏，现为 `Thread.Yield()`）。

输入侧的框架约束：pending 输入每渲染帧改写一次，`SendBufferedInput` 只在 tick 前进时把未确认输入整批上行，队列深度由客户端按 jitter 与缓冲水位自适应调节生成速率维持。本项目未覆写 `GetDefaultInput`，而 `GetDefaultInput` 只在控制器构造与客户端 pending 复位处取值：服务端输入队列排空的 tick 根本不调 `ApplyIncomingInput`，`CurrentInput` 保持上一 tick 值——空档期是**末值保持**而非回落静止。代价是输入流停摆（客户端卡发送但未断开）时单位按末值持续位移：`PawnLogic.Update` 每 tick 无条件把 `CurrentInput` 转给战斗世界，领域侧的按帧作废管不到这一层，缺的是转发时效，不是输入复位。

D6（缓冲水位）。`RoomBattleClient.BufferLowestSeconds/BufferHighestSeconds` 把 `PreferredBufferTimeLowest/Highest` 重设为 0.002/0.006 秒。LES 用同一水位同时约束下行插值缓冲与服务端输入队列，下界实算 `NetworkJitter × 1.5 + Lowest`；框架默认 0.025 在 128 Hz 下折成 3.2 tick，本地回环里 `TickLag` 的 `debt` 与 `queue` 两段几乎全由它撑起。该水位只够本地链路，公网须按 RTT 与抖动分档。

D8（输入顺序）。`main_scene.tscn` 给 `BattleCoordinator` 设 `process_priority = -1`，输入提交先于 `GameClientDriver` 的 `EntityManager.Update`，本帧 pending 在紧随的 tick 内即被采纳；代价是同一 `Tick` 里的 `BattleInputController.UpdateRaycast` 取上一帧相机位。

缺陷编号不复用。

## 可复现性的边界

回滚重放只执行被登记、且本地控制、且在 `AliveEntities` 内的实体的 `entity.Update()`，重放期间他人字段不随 `_tick` 演进。这决定了哪些结算能进预测。

| 结算内容 | 能否进预测 | 依据 |
|---|---|---|
| 自身位移、静态障碍推挤、边界钳制 | 能 | 只依赖自身输入与静态几何；两端由同一 `BattlefieldLayout` 构建 `PhysicsMovementScene`，推进与推挤只读静态形状 |
| 与其他移动单位的互斥推挤 | 不能 | 需要同帧全体意图的联合迭代；他人位置在重放中恒定，不复现让位 |
| 敌方 AI、目标选择、仇恨 | 不能 | 依赖他人实时状态与权威仇恨表；在线端不推进 AI，仇恨只下行不回填 |
| 伤害、Buff、读条推进 | 不能 | 服务端权威已单独结算并下行 |
| 瞬时命中查询 | 能，但只在滞后补偿窗口内 | 一次窗口一个结论，不构成连续约束 |

`MovementMath.ResolveExclusion` 需要同帧全体意图的联合迭代，回滚重放中他人位置恒定，让位不复现。写脏他人 `Value` 不带来预测收益，预测要写的只有自己那一份。

`SyncVar` 的两种读数在框架内按实体归属取不同代入方向：本地控制实体用 tick 内 `_lerpFactor`，远端用 `_remoteLerpFactor`。本项目未标注 `Interpolated`（D9），这条通道当前不可用，展示读的是下行 `Value`。

## 滞后补偿

作用：让判定发生在发起者当时看到的时刻。客户端按屏幕上的目标位置发起动作，等到权威侧计算时目标已经走开，不补偿就会出现「打中了没掉血」。

闭环四步：客户端每 tick 把当时的 A/B tick 与插值进度记进输入包头并上行；服务端每 tick 为带 `LagCompensated` 的实体写一格滚动历史；服务端消化该玩家输入时先恢复他当时的视图；判定前后成对调用 `EnableLagCompensationForOwner` 与 `DisableLagCompensationForOwner`，把他人字段临时替换为按该视图混合的历史值。

四条硬约束：

- 只有 A、B 两格可混，索引由框架按输入包头决定，用户无法指定任意 tick；
- 请求时刻超出状态区间时只记 `LagCompensationMiss` 日志并放弃补偿，不抛错；
- 窗口全局独占，客户端仅在回滚态可开，且不给实体自己补偿自己；
- 窗口内 `InterpolatedValue` 直返 `Value`，展示代码不得落在这个作用域里。

窗口每次 Load 与 Undo 全量遍历带该标记的实体，开销按单位数乘窗口长度计。它是判定层的瞬时机制，不是模拟层的历史数据源，也不降低权威纠正量。

本项目可对接的判定是技能命中预检：`SkillCastValidator.CanCast` 与范围伤害的圆与圆判定。代价是字段一旦标注 `LagCompensated`，服务端立刻每 tick 为其写历史，即使尚无使用点。

## 官方示例对照

结论取自 `.doc/LiteEntitySystemUnityExample-main`，与本项目的差异是设计取舍的根据。

- 示例的预测位移只有一行运动学积分，`BasePlayer.Update` 中 `_position.Value += _velocity * EntityManager.DeltaTimeF`，不含任何碰撞响应；服务端与客户端注册同一个 `BasePlayer`，因此两端逐位一致。示例能做预测的前提是权威侧也不用物理做连续解算。
- 玩家 GameObject 用 Kinematic 刚体加 trigger 碰撞体，只为射线提供几何，不产生物理响应；位置由网络值直接写 transform。
- 物理只在瞬时查询中使用，且严格包在补偿窗口内；窗口外不参与移动。
- 物理世界步进收在带 `UpdateOnClient` 的 `SingletonEntityLogic` 中按 `DeltaTimeF` 执行。本项目不需要对应物：`PhysicsMovementScene` 从不跑动力学，只做静态查询。
- 远端实体在客户端于 `Update` 首行早退，`UpdateOnClient` 的用途是取得 `VisualUpdate`；副作用与外发统一用 `EntityManager.InNormalState` 门控。
- 需要即时表现的播报走 `ExecuteFlags.ExecuteOnPrediction` 的 RPC，本地先执行、服务端确认后转发他人；本地生成的对象走 `AddPredictedEntity` 与服务端实体配对。本项目施法与命中反馈目前完全依赖可靠事件日志下行，是操控延迟的独立来源。

两处不要照抄：示例射线依赖视图脚本每帧写入的 transform 位置；`ClientLogic.Update` 与视图脚本的执行顺序不保证。本项目的判定用领域位置自算几何，不依赖视图脚本；展示取数当前落在 `EntityManager.Update` 开头，即 D10。

## 观测与读数

读数含义与边界来自框架实现，与是否实施预测无关。观测入口用门面透出的 `GameClientService.RoomNetworkStatus`（源为 `RoomBattleClient.NetworkStatus`）与 `NetworkDebugOverlay`：`LerpBufferCount` 稳态为 1，长期 0 才是插值饥饿；`Spread` 跳到 2 以上说明水位压过头导致缺号，缺号不可逆，必须回退一档；`StoredCommands` 持续增长说明服务端消化不动或未发送输入。

`BattleEntityMetrics` 只装 `ClientEntityManager` 的直读原始值，三段分解与单位换算由该类型自带的只读属性给出：生产侧不加工，消费侧不计算，新消费方拿原始值可自行复算。`AckLagTicks` 即 `LocalTick` 减 `SrvAckTick`，拆为 `UplinkTicks`（上行在途）与 `ServerQueueTicks`（服务端已收未消化）；`TickMs` 给出单 tick 宽度，本房间 7.8 ms。`AckLag` 与 `RTT` 的差额即服务端排队与缓冲代价，由 `RoomBattleClient` 的缓冲水位与房间线程的发包相位共同决定。`Loss` 是自连接起的累计丢包率，`OutRelQ` 是本端等待服务端确认的出站可靠包数；可靠事件日志那条独立延迟发生在服务端出站队列，客户端读不到。

读数约束：tick 有两套起点，`LocalTick`/`SrvAckTick`/`SrvRecvTick` 随客户端计数（服务端只回显后两者），`ServerTick`/`SrvStateTickA`/`B` 随服务端计数，跨套相减无意义；`ServerTick` 还是 A/B 间的插值读数且量化到整 tick。回显的两个 tick 只随 diff 状态下发、baseline 不带，而 `ServerStateData.Reset` 不清这三个字段、状态对象又来自对象池，故每次重同步后可能读到上一段会话的残值——`TickLagTrusted` 为假时三段与净回环都不给读数。`Loss` 只计本端发送侧，下行 unreliable 丢包在屏上不可见；单向下行耗时也不可测——LES 下行包头无时间戳、LiteNetLib 2.1.4 无对表接口，`one-way` 只是 RTT 半值估计。

下行状态流健康看 `Spread`：它是正在播的 A 与目标 B 的服务端 tick 差，正常恒为 1。LES 的插值节拍 `_remoteInterpolationTotalTime` 与该差成正比，一旦跳到 n，消费速率跌到 tickrate/n，低于服务端产出速率，`LerpBuf` 开始积压，直到 30（`MaxSavedStateDiff`）才靠强制快进止血。跳号的来源是 `Deserialize` 把晚到的状态按 `tickDifference <= 0` 整包丢弃，号就此永久缺。追赶能力别高估：`GetSpeedMultiplier` 的 `InvLerp` 带 clamp，缓冲再长也只加 10% 节拍。

`TickLag` 偏高先拆两半：`net` 是上行与服务端耗时，`debt` 是画面落后自己已收到数据的时长（`LerpBuf × state every`）。三段读数取自 state A 的回显，A 一旦积压就整体抬高它们，所以 loopback 下 `RTT 0` 而 `AckLag` 仍大，几乎必然是 `debt` 撑起来的，不是网络。

本文时序与缺陷表随代码同步维护。同一条链路的机制各有归属：帧处理与收包分流在 `overview/client`，同步通道与搬运规则在 `overview/battle`，端到端次序在 `flow/battle-state-sync`。本文只保留预测视角的判据与缺陷编号，不复述他处机制。
