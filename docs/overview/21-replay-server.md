# DungeonChessBattle.Replay.Server

回放服务侧：HTTP 端点、查询与鉴权。职责边界见 `functional_boundary/21`。

## 调用链

```
GET /replay/list     ─┐  请求头 X-Dcb-Session
GET /replay/{roomId} ─┘ → ResolveRecord → IPlayerIdentityResolver → 玩家记录主键
                                                    ↓
                                          ReplayServer → IReplayStore
```

宿主只有一句 `app.MapReplayEndpoints()`，路由与鉴权都在本库，宿主不认识回放概念。

## 身份边界

- 端点入参只有请求头里的会话凭证。本层不认识"登录"这个动作，也不认识连接：解析端口换成记录主键，换不到就 401。
- 记录主键是回放归属的唯一口径：归档方（Server.Battle）按玩家名解析主键写入，查询方按凭证解析主键读取，两侧同一解析实现才不会错位。
- 会话凭证由服务端签发、可换发可撤销，比客户端自报玩家名强一层；但大厅 Hub 上的业务身份仍是自报的，**这层加固不覆盖那里**。

## 状态码口径

- 401：缺凭证，或凭证已随连接作废。
- 404：非参与者、房间 ID 非法、归档不存在——三者同回 404，不暴露回放存在性。
- 命中才回字节：`Results.File` 直接输出归档，文件名 `{roomId}.replay`，不经 JSON。

## 数据能活多久

归档、会话凭证与玩家记录主键都是进程内数据，服务端重启一并消失。重启后还能播的回放只存在于客户端本地缓存——服务端不提供"历史回放库"，这是当前形态的既定边界，不是待补的漏洞。
