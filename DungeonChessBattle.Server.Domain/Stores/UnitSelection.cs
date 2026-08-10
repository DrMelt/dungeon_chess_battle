namespace DungeonChessBattle.Server.Domain.Stores;

/// <summary>
/// 准备阶段的一单位选择记录（只读快照项）。
/// </summary>
/// <param name="UnitName">单位名。（显示名）</param>
/// <param name="Camp">所属阵营。</param>
/// <param name="PlayerName">选择该单位的玩家名。</param>
/// <param name="PlayerId">选择该单位的玩家持久标识（控制器绑定用权威键）。</param>
public sealed record UnitSelection(string UnitName, string Camp, string PlayerName, string PlayerId);
