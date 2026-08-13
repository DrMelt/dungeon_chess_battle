namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 技能类型强类型 ID。领域、判定与配置层使用，杜绝裸 ushort 造成的类型混淆；
/// 网络协议与同步实体边界保持 ushort，并在此类型 .Id 处显示转换。
/// </summary>
public readonly record struct SkillKeyId(ushort Id);
