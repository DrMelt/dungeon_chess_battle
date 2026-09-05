using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.GameConfig.Models;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 单位目录契约：配置键 ↔ 单位配置。返回的 <see cref="UnitConfig"/> 已含逻辑定义
/// （AI/仇恨/技能效果），消费方取用即运行，不自行实例化行为。
/// </summary>
public interface IUnitRegistry {
    /// <summary>全部单位配置。</summary>
    IReadOnlyCollection<UnitConfig> All {
        get;
    }

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    UnitConfig? GetByKey(UnitConfigKey configKey);

    /// <summary>按 UnitConfig 引用反查是否为已注册单位；未注册返回 null。</summary>
    UnitConfig? GetByConfig(UnitConfig config);
}
