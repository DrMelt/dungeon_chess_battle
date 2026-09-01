# DungeonChessBattle.Replay.Protocol

回放 HTTP 契约：DTO、路由与序列化约定。职责边界见 `functional_boundary/23`。

## 为什么单独一层

回放自成一组 HTTP 端点，与大厅的协议和 DTO 不同源。服务端映射端点与客户端调用要知道同一批路径、同一批字段形状，写两遍就会漂移，因此单列一层契约库。

## 契约内容

| 契约 | 作用面 |
| --- | --- |
| `Dtos/ReplayDtos.cs` | 摘要列表 `ReplayListResult`、摘要条目（含时长与两项修订号）及其 `From(ReplayMeta)` 投影、参与玩家条目 |
| `ReplayHttpRoutes` | 路径前缀与两条路由、会话凭证头名、URL 组装 |
| `ReplayJson` | DTO 的 `JsonSerializerOptions`：驼峰命名 + 读取大小写不敏感 |

`ReplayJson` 不是便利品：序列化不再经 SignalR，两端各配一份选项就会出"字段静默为 null、日志里查不到原因"的故障，唯一来源是必需的。

`ReplaySummaryDto.From` 同理，只是原因换了个方向：同一份归档元数据有两条路上线——服务端读归档现投、客户端读本地副本自投。字段清单写两遍，加一个字段就会漏一半，症状是同一行卡片换个来源就少个值，且只有那半边路径发作、最难查。两条路径因此合流于 `From` 之后，来源差异只剩 `FromServer` 一个布尔。

不上线的东西也不进 DTO：`ReplayMeta` 的 `StartTick`/`EndTick` 折成 `DurationTicks`，玩家表的 `NetId` 直接不列，归属用的玩家记录主键更是只活在服务端索引里。归档可以演化，wire 形状不必跟着抖。

## 谁命名了那个请求头

会话凭证由大厅登录签发、随大厅连接作废，但 `X-Dcb-Session` 这个头名声明在回放侧：端点只说"我接受哪个头自证"，签发方从不引用它。两个半边各自命名，谁也不知道对方叫什么——这正是回放与大厅脱钩的样子。

## 没有的部分

回放不跨库传接口：服务端端点自己解析凭证，客户端自己发 HTTP，两侧之间只有报文。曾经的 `IReplayApplication`（供 Hub 委托）与 `IReplayRequestChannel`（供借大厅连接）随 Hub 通道一起退场。

## 依赖方向

`Replay.Server` 与 `Replay.Client` 都指向本库取契约，两边也各自指向 `Replay.Shared`——前者读归档现投摘要，后者解码归档供重放。本库只指向 `Replay.Shared` 取 `ReplayMeta` 这个源形状，不指向任何运行时库；大厅两侧不引用本库。回放分组因此可独立编译与替换。
