namespace DungeonChessBattle.Battle.Domain.Enums;

/// <summary>
/// 以函数形式声明的阵营关系判定，由各副本在配置中定义。
/// 需要覆盖战斗中出现的一切阵营组合；配置缺失分支会使注册期自检失败。
/// </summary>
/// <param name="sourceCamps">源单位所属全部阵营。</param>
/// <param name="targetCamps">目标单位所属全部阵营。</param>
public delegate CampRelation CampRelationResolver(
    IReadOnlyList<string> sourceCamps, IReadOnlyList<string> targetCamps);
