# 客户端回放设计

回放的本质是：一份自包含字节流 + 一段可复现的确定性重算，在无网络、无实时输入、无权威服务器参与下，原样复现一场已结束战斗的完整表现。观看者不提供输入。

本文跨 `Replay.Shared`、`Replay.Protocol`、`Replay.Server`、`Replay.Client`、`Replay`、`Battle.Server`、`Game` 多模块，描述回放的确定性契约与获取链路。模块职责见 `functional_boundary/16`、`18`、`21`、`22`、`23`，在线并行的同步链路见 `battle-state-sync.md`。

## 获取链路

录制→归档→获取→重放四段各自归位，跨界只在契约：

```
Battle.Server 命令录制 → IReplayStore 归档（摘要含 DataVersion）
  → 大厅 Login 成功签发会话凭证 → 客户端持有，随连接作废
  → GET /replay/list、GET /replay/{roomId}（请求头带凭证）
  → Replay.Server 端点：凭证 → 玩家记录主键 → 参与者校验 → 摘要 / 字节流
  → Replay.Client 取字节流 → Game.ReplayService 解码 + 版本门控 + 并集/缓存 → Replay 引擎重跑 → UI
```

- 回放不借大厅连接：请求全走 HTTP，身份由服务端签发的会话凭证自证；大厅只签发凭证、不认识回放，回放只解析凭证、不认识登录，两侧只剩报文。
- 归档与本地缓存：归档、会话凭证与玩家记录主键都只在服务端进程内存活，客户端缓存是跨进程唯一副本；故列表取"服务端 ∪ 本地"，服务端重启后已下过的回放仍可列可播。
- 身份以端口隔离：`Replay.Server` 只经 `IPlayerIdentityResolver` 把凭证换成玩家记录主键，归档方与查询方同用一解析，口径不错位。

## 单一真相源复用

- 权威状态由 `BattleScene`（`Battle.Logic`）持有的领域实体 `BattleUnit` 决定，战斗房间服务与回放引擎共用，不依赖网络载体。
- 回放 `ReplayEngine` 构建 `BattleScene`（不投影）；移动在 `BattleScene.Tick` 内统一结算，与在线同源。
- 领域层与展示契约一致：在线经"领域→`BattleStateSynchronizer`→LES→载体→领域回填→`BattleUnit`→UI"，回放"领域→`BattleUnit`→UI"，都收敛到 `IUnitUiView`/`IBuffUiView`，UI 不感知来源。仅在线多一层同步/回填。

## 确定性契约

输入重放的成立依赖确定性与数据一致，二者都必须是契约而非假设：

- 逻辑确定性：AI/伤害/移动均为纯函数无随机（`Battle.Logic/Shared/Server` 无 `Random`），固定逻辑步长，可以重算断言。
- 输入形状唯一：`PlayerCommand`（Battle.Shared）是三类玩家输入的唯一形态。在线把请求转成命令交门面，命令同时被录制器落成条目；重放把条目还原成命令交同一个门面。载荷拆分（如施法的单位目标与位置锚点取舍）只有一份实现，判定（阶段、技能键、ID 解析、聚焦目标存活）也只在门内一次，两端同判是复现权威结论的前提。
- 内容一致性：`ReplayRecordHeader.DataVersion` 为录制端 `GameConfigDB.DataRevision`。门控两处：`Game.ReplayService` 下载解码后即判不可播放且不落缓存，`ReplayEngine` 构造再校验一次，引擎不信任输入。归档摘要与本地缓存条目都从记录头部携带 `DataVersion`，列表侧可在下载前标注。任何影响战斗结果的变更都必须递增 `DataRevision`——内容侧是配置与布局，引擎侧是结算时序与事件顺序。
- 同帧注入先后：施法与移动都只登记意图，裁定在 `BattleScene.Tick` 内单点完成；仍需同序的只剩门面 `PrepareTick` 的在架重试先于本帧新按键，见 `overview/07` 输入门面。
- 格式版本：`ReplayFormatVersion.Current` 门控记录模型，模型或编码变化时递增，`ReplayRecordCoder` 解码校验；客户端列表按它过滤本地旧副本，版本不符的条目不展示。

播放控制：`ReplayCoordinator` 以固定步长累积器驱动引擎，`SeekTo` 重置到首帧快进。

## UI 归属

`Game/Replay/` 一场景一目录、一所有者：`ReplayPanel` 取数与呈现，不碰屏幕态；`ReplayItem` 暴露下载与播放两按钮、上报房间 ID；`ReplayHud` 只管播放控制；`ReplayCoordinator` 管引擎生命周期与表现绑定。启动回放仅由 `ReplayPanel` 对播放按钮触发，后台获取完成不自动进入。

入口面板是前厅页面之一，由 GameLobby 经 `BaseGamePanel` 导航链打开，启动播放后自行返回，故退出回放落回大厅——落点归导航链，FrontUI 与在线战斗 UI 的显隐归 `ScreenStateMachine`，它不认识任何具体面板。

## 预留改进

- 事件反馈消费：`ReplayEngine.Step()` 返回的 `IBattleEvent` 流已由 `Game` 侧复用在线 `UnitStateChangeInfo` 消费，弹出与在线共用的受击/治疗/Buff 浮字；倍速下逐帧消费的观感与在线插值平滑度仍待补。
- 关键帧快照：`SeekTo` 反向跳现为 O(n) 从首帧快进；后续以周期 keyframe 快照就近重建，跳转与确定性校验都可从任意点起步。
- 展示插值：回放逐 tick 直读权威位，低 tick 观感跳格；如需与在线渲染同平滑度，补渲染层插值。
- 记录体积：移动输入按玩家每 tick 一条落盘，是不设上限的记录里唯一的增长源——128 tick/s 下八人 10 分钟约 74 万条，运行期常驻房间线程内存。压缩方向是只记变化沿，但那要求重放端持有末值跨帧，与「意图在 `Tick` 末作废」的既有契约冲突，动手前须一并决定旧录像去留。
