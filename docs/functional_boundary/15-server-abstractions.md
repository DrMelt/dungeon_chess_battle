# DungeonChessBattle.Server.Abstractions

服务端抽象契约，所属分组 Server。定义服务端各领域模块与装配层之间协作的纯接口，只暴露原语类型，不依赖任何领域实现。

## 职责

- 战斗房间服务器生命周期契约：开始战斗、端口查询、玩家重连登记、空房清理、停止与列表。
- 大厅广播端口：向房间内连接分组推送消息。
- 会话身份解析端口：由服务端签发的会话凭证换取玩家记录主键。
- 回放归档存储端口：以房间 ID 为主键写编码字节流与摘要、按玩家记录主键检索、按房间取字节流。

## 边界外

- 不包含领域类型，入参与返回值限原生类型、字符串与纯原语 DTO。
- 不包含录制与重跑逻辑，录制在 Battle.Server.Replay、重跑在 Replay 引擎；不包含 HTTP 端点与网络栈，归 Replay.Protocol 与 Replay.Server。
- 不包含实现：广播、房间生命周期由 Lobby.Server 与 Battle.Server 实现，会话凭证与身份解析由 Server.DataStore 实现。
- 不承担凭证的签发与撤销：那是大厅登录流程与存储层的动作，本端口只解析已签发的凭证。
- 不覆盖大厅自身的鉴权：Hub 上的业务身份仍是登录时自报的玩家名，服务器对外暴露前需先加固登录。

## 依赖

- 无。零依赖契约库，供 Lobby.Server、Battle.Server、Replay.Server、Server.DataStore 与 Server.Host 共享。
