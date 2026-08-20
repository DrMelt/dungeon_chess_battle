namespace DungeonChessBattle.Server.StateStore.Abstractions;

/// <summary>
/// 准备阶段的一单位选择记录，只读快照项。
/// 不携带实际阵营列表，阵营由副本配置按选项键权威解析。
/// </summary>
/// <param name="UnitConfigKey">单位配置键，与 UnitConfig.ConfigKey 一致。</param>
/// <param name="CampOptionKey">玩家阵营选项键，对应副本配置 PlayerCampOptions 中的选项。</param>
/// <param name="PlayerName">选择该单位的玩家名。</param>
/// <param name="PlayerId">选择该单位的玩家持久标识，控制器绑定用权威键。</param>
public sealed record UnitSelection(
    string UnitConfigKey,
    string CampOptionKey,
    string PlayerName,
    string PlayerId);
