using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Config.Shared.Content;

namespace DungeonChessBattle.Battle.Config.Registry;

/// <summary>
/// 单位目录接口：配置键 ↔ 单位配置。返回的 <see cref="UnitConfig"/> 已含 AI、仇恨规则与技能定义引用，
/// 消费方直接取用。
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
