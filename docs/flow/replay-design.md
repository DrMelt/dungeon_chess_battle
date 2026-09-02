# 客户端回放设计

回放的本质是：一份自包含字节流 + 一段可复现的确定性重算，在无网络、无实时输入、无权威服务器参与下，原样复现一场已结束战斗的完整表现。观看者不提供输入。

本文跨 `Replay.Shared`、`Replay.Protocol`、`Replay.Server`、`Replay.Client`、`Replay`、`Battle.Server`、`Game` 多模块，描述回放的确定性契约与获取链路。模块职责见 `functional_boundary/16`、`18`、`21`、`22`、`23`，域内机制（容器格式、条目映射、状态码口径）见 `overview/replay`，归档的存储结构与淘汰见 `overview/datastore`，在线并行的同步链路见 [battle-state-sync](battle-state-sync.md)。

## 获取链路

录制→归档→获取→重放四段各自归位，跨界只在契约：

```
Battle.Server 命令录制 → IReplayStore 归档（字节流 + 参与者主键，摘要在归档自身）
  → 大厅 Login 成功签发会话凭证 → 客户端持有，随连接作废
  → GET /replay/list、GET /replay/{roomId}（请求头带凭证）
  → Replay.Server 端点：凭证 → 玩家记录主键 → 参与者校验 → 元数据块 / 字节流
  → Replay.Client 取字节流 → Game.ReplayService 解码 + 双重门控 + 并集/缓存 → Replay 引擎重跑 → UI
```

- 回放不借大厅连接：请求全走 HTTP，身份由服务端签发的会话凭证自证；大厅只签发凭证、不认识回放，回放只解析凭证、不认识登录，两侧只剩报文。
- 归档与本地缓存：归档、会话凭证与玩家记录主键都只在服务端进程内存活，客户端缓存是跨进程唯一副本；故列表取"服务端 ∪ 本地"，服务端重启后已下过的回放仍可列可播。
- 身份以端口隔离：`Replay.Server` 只经 `IPlayerIdentityResolver` 把凭证换成玩家记录主键，归档方与查询方同用一解析，口径不错位。

## 单一真相源复用

回放与在线共用同一 `BattleScene`、同一输入门面与同一展示契约，两条链的收敛点写在 `flow/battle-state-sync` 的「回放链与两链收敛」一节。本文只关心成立条件：回放不投影、领域直读，无网络与投影层，因此重跑结果只由输入与配置决定。

## 确定性契约

输入重放的成立依赖确定性与数据一致，二者都必须是契约而非假设：

- 逻辑确定性：AI/伤害/移动均为纯函数无随机（`Battle.Logic/Shared/Server/GameConfig` 无 `Random`），固定逻辑步长，可以重算断言。
- 输入形状唯一：`PlayerCommand`（Battle.Shared）是三类玩家输入的唯一形态。在线把请求转成命令交门面，命令同时被录制器落成条目；重放把条目还原成命令交同一个门面。载荷拆分（如施法的单位目标与位置锚点取舍）只有一份实现，判定（阶段、技能键、ID 解析、聚焦目标存活）也只在门内一次，两端同判是复现权威结论的前提。
- 内容一致性：两项修订号都要对上——`DataVersion` 是录制端 `GameConfigDB.DataRevision`（配置与布局），`LogicVersion` 是录制端 `BattleLogicRevision.Value`（结算时序与事件顺序）。门控两处：`Game.ReplayService` 下载解码后即判不可播放且不落缓存，`ReplayEngine` 构造再校验一次，引擎不信任输入。归档与本地缓存条目都从元数据块携带两项修订号，列表侧可在下载前标注。任何影响战斗结果的变更都必须递增对应修订号。
- 同帧注入先后：施法与移动都只登记意图，裁定在 `BattleScene.Tick` 内单点完成；仍需同序的只剩门面 `PrepareTick` 的在架重试先于本帧新按键，机制见 `overview/battle` 的输入门面一节。
- 世界重建同源：归档带全部单位的初始态表（ID、阵营、出生点），重放端照表建世界，不再依赖"实体 ID 连续分配"这类运行期前提；单位属性仍按配置键取当前配置，避免与配置双真相。
- 存储不改输入语义：移动按玩家分轨收拢为方向意图段，折叠判据与轨道成型同载于 `ReplayCommands`（与载荷拆分一处，录制端不碰条目形状），重放端逐帧展开重投，与在线"输入源逐 tick 重投、`Tick` 末作废"同构；分量 bit-exact，量化会让确定性斜坡换一条。
- 格式版本：`ReplayArchive.FormatVersion` 变化即拒绝旧读侧，解码与只读元数据两条路径都校验；客户端列表按它过滤本地旧副本，版本不符的条目不展示、也不占重下名额。容器与块语义的升降规则见 `overview/replay`。

播放控制：`ReplayCoordinator` 以固定步长累积器驱动引擎，`SeekTo` 重置到首帧快进。Godot 侧的面板归属、屏幕态仲裁与导航落点见 `overview/godot` 的回放表现归属一节，本文不重述。

