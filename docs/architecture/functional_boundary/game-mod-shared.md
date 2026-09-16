# DungeonChessBattle.Game.Mod.Shared

展示接口层：装配上下文与表现接口——装配上下文由宿主递交、mod 消费，表现接口由 mod 实现、宿主调用。注册表读写口随注册表库发布，不在本库。只依赖 Game.Display.Registry，不依赖 Game.Mod.Interface。

## 职责

- 装配上下文：入口初始化时携带 mod 归属与注册表；包内资源由 mod 自行加载。
- 表现接口：mod 侧提示场景实现，宿主按口驱动预览，宿主不认识场景的脚本类型与节点结构。

## 边界外

- 不含 mod 入口接口：在 Game.Mod.Interface。
- 不含注册表读写口与实现：读写口与实现在 Game.Display.Registry。
- 不含包内资源加载端口：mod 侧读自己包内文件不经宿主中转。
- 不含展示数据形状：四类展示数据在 Game.Shared。
- 不含装载与管理逻辑：扫描、启停、展示装配在 Game.Mod.Manager。

## 依赖

- Game.Display.Registry。
