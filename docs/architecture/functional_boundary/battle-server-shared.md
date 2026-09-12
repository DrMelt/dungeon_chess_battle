# DungeonChessBattle.Battle.Server.Shared

战斗域服务端共享接口库。定义战斗域服务端与外部消费方之间协作的纯接口，只暴露原语类型，不依赖任何领域实现。

## 职责

- 房间生命周期接口：开始战斗、端口查询、玩家重连登记、空房清理、停止与列表。

## 边界外

- 不含实现：广播与战斗房间服务器由各域实现，本层只有接口。
- 不包含战斗域的数据结构：归 Battle.Shared。
- 不包含网络传输类型，只暴露端口等原语。

## 依赖

- 无：纯 .NET 接口库，供 Battle.Server 实现、Lobby.Server 与 Server.Host 消费。
