using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Movement;
using DungeonChessBattle.Battle.Shared.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 副本目录契约：副本键 ↔ 副本配置与布局。返回的 <see cref="DungeonConfig"/> 已含阵营关系、
/// 敌方阵容与移动布局，消费方据此构建战斗世界。
/// </summary>
public interface IDungeonRegistry {
    /// <summary>默认副本键：未指定副本或副本键未注册时使用，由内容侧定义、本登记点暴露。</summary>
    string DefaultDungeonKey {
        get;
    }

    /// <summary>全部副本配置。</summary>
    IReadOnlyCollection<DungeonConfig> All {
        get;
    }

    /// <summary>按副本键获取配置；不存在或为空返回 null。</summary>
    DungeonConfig? GetByKey(string? dungeonKey);

    /// <summary>按副本键获取移动场景布局；未配置或不存在返回默认竞技场。</summary>
    BattlefieldLayout GetMovementLayout(string? dungeonKey);

    /// <summary>按权威副本键获取阵营关系函数；未知键抛异常。</summary>
    CampRelationResolver GetRelations(string dungeonKey);
}
