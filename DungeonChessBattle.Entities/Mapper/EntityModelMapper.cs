using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Entities.Mapper;

/// <summary>
/// 实体 ↔ Model 转换映射器，集中管理 UnitSyncEntity → UnitModel 的转换逻辑。
/// </summary>
public static class EntityModelMapper {
    /// <summary>
    /// 将网络同步实体转换为 Logic 层的 UnitModel。
    /// </summary>
    public static UnitModel FromSyncEntity(UnitSyncEntity entity) {
        return new UnitModel {
            UnitStateName = entity.UnitName.Value,
            Health = entity.Health.Value,
            MaxHealth = entity.MaxHealth.Value,
            PhysicalAttackBase = entity.PhysicalAttackBase.Value,
            MagicAttackBase = entity.MagicAttackBase.Value,
            PhysicalTakePercent = entity.PhysicalTakePercent.Value,
            MagicTakePercent = entity.MagicTakePercent.Value,
            CureIntensity = entity.CureIntensity.Value,
            BaseSpeed = entity.BaseSpeed.Value,
        };
    }
}
