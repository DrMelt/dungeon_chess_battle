# DungeonChessBattle.Server.Host

服务器可执行宿主。服务端宿主框架装配层，游戏服务器进程入口，不含领域业务实现。

## 职责

- 入口装配：读取启动参数并启动宿主。
- 内容装配：进程启动时扫描 mod 目录并执行数据面装配，先于任何房间创建，产物分发给大厅与房间。
- 依赖装配：绑定进程内共享依赖，含存储实现、身份解析端口与内容注册表；统一配置映射为各模块配置切片；装配大厅、战斗、回放三侧模块与长连接、回放端点。
- 房间生命周期驱动：以固定节拍驱动空房清理，停止时先停清理循环再停全部房间。
- 进程看护：父进程已消失则不启动，运行中消失触发优雅退出。

## 边界外

- 不实现业务逻辑：大厅、战斗、回放、存储全部委托下层。
- 不含子进程管理：拉起与停止由引擎端承担，本侧只响应父进程看护约定。
- 不定义服务端领域接口：房间生命周期接口在 Battle.Server.Shared，存储接口在 Server.DataStore.Shared。

## 依赖

- Battle.Config.Registry、Battle.Entities、Battle.Server.Shared、Server.DataStore.Shared、Lobby.Protocol、Lobby.Server、Battle.Server、Replay.Server、Battle.Mod.Manager、Server.DataStore。
