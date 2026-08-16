# DungeonChessBattle.GameConfig

单位与副本配置库，所属分组 Shared。纯 C# 配置数据库，零反射，编译期类型安全，服务端与客户端共用同一套配置。

## 职责范围

- 静态配置数据：Buff、技能、单位、副本及其模型。
- 权威登记点：`UnitRegistry` 与 `DungeonRegistry`，配置键与配置模型映射。
- 读取接口：`IGameConfigDB` 契约，`GameConfigDB.Instance` 供 Godot 脚本访问。

## 不负责

- 不含配置消费逻辑，敌人生成、结算、展示全由消费方实现。
- 不做运行时行为与反射，配置即数据。
- 不越过登记点，新增单位/副本必须经 Registry 登记。

## 与周边协作

- 下游：Server.Lobby（副本键解析）、Server.Battle（敌人生成与布局）、Client.Battle（移动场景）、Godot 主工程（单位展示）。
