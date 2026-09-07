# 文档分层与维护规则

总则：

1. 同一事实只允许有一处，别处写路径指向它。
2. 读代码五分钟就能得到的东西不进任何一层：字段清单、方法签名、目录列表。

## 分层

| 层                                | 每篇覆盖           | 写什么                                     | 不写什么             |
| --------------------------------- | ------------------ | ------------------------------------------ | -------------------- |
| `architecture.md`                 | 一个工程           | 项目划分、依赖方向、模块索引               | 机制与时序           |
| `functional_boundary/<slug>.md`   | 一个模块           | 职责、边界外、依赖                         | 实现细节、字段、时序 |
| `overview/<域>.md`                | 一个域             | 域内机制、破坏即出缺陷的域内约束           | 跨域才成立的链路     |
| `flow/<链>.md`                    | 一条端到端链       | 跨了哪些模块、按什么次序、错了什么现象     | 单模块内部机制       |
| `libraries/<库>.md`               | 一个第三方库       | 库自身的行为与时序                         | 本项目的用法         |
| `mod-development.md`（外部指南）   | mod 开发者         | 包与源、工程要求、manifest 契约、注册面用法、版本纪律 | 装配机制与实现细节   |

## 域与链

- 域（对应 `overview/<域>.md`）：
  - `godot` 主工程装配与表现；
  - `client` 客户端装配与契约；
  - `battle` 战斗世界、房间服务、在线端与配置登记；
  - `lobby` 大厅与大厅客户端；
  - `datastore` 状态存储与身份凭证；
  - `replay` 回放子系统；
  - `server` 服务端装配与契约；
  - `mod` 内容装载、mod 管理与展示装配。
- 链（对应 `flow/<链>.md`）：
  - `battle-state-sync` 权威状态下行；
  - `connection-reconnect` 启动—进房—重连—收敛；
  - `replay-design` 录制—归档—获取—重放；
  - `client-prediction` 在线预测调查与缺陷登记。

## 内容放错层时怎么处理

内容按本性归位：链路归 flow、机制归 overview、边界归 functional_boundary。

- flow 里出现只讲一个模块的实现细节：这就是 overview 的内容，搬过去。
- overview 里逐模块复述端到端次序：压成一行，指向 flow。

反向不成立：不要为了合并把域内机制塞进 flow。

## 命名与引用

- 命名：`functional_boundary` 用 slug（`battle-logic`），`overview` 用域名，`flow` 用链域名；文件名即模块身份。外部指南放 docs 根目录，文件名即面向对象（`mod-development`）。
- 跨文档引用：只写目录与文件名（`functional_boundary/battle-logic`、`overview/battle`、`flow/battle-state-sync`），不写锚点，改标题不断链。
- 指向某一节：用文字「的某节」，不用 `#`。
- 同一文件内不写这种跳转，约束就地写全。
