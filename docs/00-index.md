# 文档分层与维护规则

同一事实只允许存在一处，其余位置写路径指向它。判据：

| 层 | 切分维度 | 只写 | 绝不写 |
|---|---|---|---|
| `architecture.md` | 一工程一篇 | 项目划分、依赖方向、模块索引 | 机制与时序 |
| `functional_boundary/NN-slug.md` | 一模块一篇 | 职责 / 边界外 / 依赖 | 实现细节、字段、时序 |
| `overview/<域>.md` | 一域一篇 | 域内部机制、破坏即出缺陷的单域约束 | 需要跨域才成立的链路 |
| `flow/<链>.md` | 一条端到端链一篇 | 跨了哪些模块、按什么次序、错了什么现象 | 单模块内部机制 |
| `libraries/<库>.md` | 一第三方库一篇 | 库自身的行为与时序 | 本项目的用法 |

## 域与链

- 域：`godot` 主工程装配与表现、`client` 客户端连接侧、`battle` 战斗世界与房间服务、`lobby` 大厅与状态存储、`replay` 回放子系统、`server` 服务端装配与契约。
- 链：`battle-state-sync` 权威状态下行、`connection-reconnect` 启动—进房—重连—收敛、`replay-design` 录制—归档—获取—重放、`client-prediction` 在线预测调查与缺陷登记。

## 发现重复时怎么删

链路归 flow、机制归 overview、边界归 functional_boundary。flow 里长出一段只讲一个模块的实现细节，就是该搬去 overview 的信号；overview 里逐模块复述端到端次序，就是该压成一行指向 flow 的信号。反向不成立——不要为了合并把域内机制塞进 flow。

## 命名与引用

- `functional_boundary` 用 `编号-slug`，编号即模块身份；`overview` 用域名，`flow` 用链域名。
- 跨文档引用只写目录与文件名（`overview/battle`、`flow/battle-state-sync`），不写锚点也不写 slug 后缀——改标题不断链。指到某一节时写成「的某节」文字，不用 `#`。

## 维护时机

- 改代码的提交只同步它实际动过的那几层：机制改动改 overview，链路改动改 flow，边界变动才动 functional_boundary。
- `client-prediction` 的缺陷编号不复用，关闭的条目留在「已关闭」段。
- 单篇上限按非空行计：overview 80、flow 40。超了先怀疑是不是把两层内容写混了。`client-prediction` 是调查记录、缺陷表随发现增长，属已知例外。
- 读代码五分钟能得到的东西不进任何一层：字段清单、方法签名、目录列表。
