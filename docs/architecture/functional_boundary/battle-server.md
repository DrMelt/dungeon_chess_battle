# DungeonChessBattle.Battle.Server

战斗房间服务层。每个房间拥有独立的实体服务器与战斗世界。

## 职责

- 单房间战斗服务：独立网络、实体管理与战斗世界。
- 房间生命周期：创建与销毁、端口分配回收与空房清理。
- 实现房间管理接口，向外只暴露端口等原语。
- 房间启动失败：副本不在场与首帧初始化超时或失败按原因交回调用方，未登记的房间停止并回收端口。
- 按副本配置初始化房间的战斗状态。
- 战斗循环：实体更新后预备本帧意图并推进战斗、权威状态同步与事件外送。
- 玩家会话与断线重连：校验连接密钥并恢复既有同名会话。
- 战斗回放：录制玩家命令并归档，房间销毁或关服时落盘。

## 边界外

- 不实现大厅业务与传输层广播。
- 不为客户端结算：结算权威都在战斗世界。
- 不承担回放执行，只负责录制与归档。

## 依赖

- Battle.Logic、Battle.Shared、Battle.Config.Shared、Battle.Runtime.Shared、Battle.Entities、Battle.Config.Registry、Replay.Shared、Battle.Server.Shared、Server.DataStore.Shared。
