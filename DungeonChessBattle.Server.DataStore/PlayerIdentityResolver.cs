using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Server.DataStore.Shared;

namespace DungeonChessBattle.Server.DataStore;

/// <summary>
/// <see cref="IPlayerIdentityResolver"/> 的存储侧实现：会话凭证与玩家记录注册表都在状态存储内，
/// 解析即两步查表。不为匿名凭证登记记录，避免无效凭证凭空产生玩家记录。
/// </summary>
/// <param name="stateStore">状态存储门面。</param>
public sealed class PlayerIdentityResolver(IGameStateStore stateStore) : IPlayerIdentityResolver {
    /// <inheritdoc />
    public string? ResolveRecordId(string sessionToken) {
        string? playerName = stateStore.GetSessionPlayerName(sessionToken);
        return string.IsNullOrEmpty(playerName) ? null : stateStore.ResolvePlayerRecordId(playerName);
    }
}
