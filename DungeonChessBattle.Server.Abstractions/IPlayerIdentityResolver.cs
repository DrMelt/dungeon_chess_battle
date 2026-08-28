namespace DungeonChessBattle.Server.Abstractions;

/// <summary>
/// 会话凭证到玩家记录主键的解析端口。
/// 边界约定：凭证由登录流程签发、随登录会话作废，解析方据此换取玩家记录主键，不认识连接与登录动作本身。
/// 凭证不透明、可换发可撤销，比客户端自报玩家名强一层；但大厅 Hub 上的业务身份仍是自报的，
/// 本端口的加固不覆盖那里，服务器对外暴露前需一并加固登录。
/// </summary>
public interface IPlayerIdentityResolver {
    /// <summary>解析会话凭证对应的玩家记录主键；凭证无效、已撤销或未登录时返回 null，不登记新记录。</summary>
    string? ResolveRecordId(string sessionToken);
}
