# DungeonChessBattle.Lobby.Client

大厅客户端，SignalR 传输。只承载大厅与准备阶段的请求与广播回调，不包含 LES 实体系统。

## 职责

- 构建连接、注册服务端回调、发起大厅与准备阶段请求。
- 事件派发：房间创建/加入、快照更新、招募板列表、重定向与连接状态事件。
- 留存服务端签发的会话凭证，经 `SessionToken` 供上层透传。

## 边界外

- 不包含战斗房间客户端与 LES 实体系统。
- 不含回放：回放的请求、下载与缓存全在回放获取侧（`Replay.Client` 出网、Game 层浏览服务编排），本层只交出一个连接级凭证，不认识它被谁消费。
- 不在回调线程触碰客户端状态，消费方负责转主线程。
- 不定义协议契约，Hub 方法与大厅 DTO 归 Lobby.Protocol，连接契约归 Client.Shared。

## 依赖

- Client.Shared、Lobby.Protocol。
