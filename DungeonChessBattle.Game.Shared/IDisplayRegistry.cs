using Godot;

namespace DungeonChessBattle.Game.Shared;

/// <summary>
/// 展示资源统一索引的查询面：内置资源与 mod 注册进来的展示数据都汇在这里，消费方按身份键取只读视图。
/// 注册经 <see cref="IModDisplayRuntime"/>，本接口只读；注册次序由装配方保证，同键后注册者覆盖先注册者。
/// </summary>
public interface IDisplayRegistry {
    /// <summary>按技能键取视图；未注册返回 null。</summary>
    ISkillView? GetSkill(string skillKey);

    /// <summary>按 BuffTypeId 取视图；未注册返回 null。</summary>
    IBuffView? GetBuff(ushort buffTypeId);

    /// <summary>按副本键取视图；键为空或未注册返回 null。</summary>
    IDungeonView? GetDungeon(string? dungeonKey);

    /// <summary>按单位配置键取视图；未注册返回 null。</summary>
    IUnitView? GetUnit(string configKey);

    /// <summary>按资源名取纹理；名未注册或解析失败返回 null。首次解析后缓存结果。</summary>
    Texture2D? Texture(string? assetId);

    /// <summary>按资源名取场景模板；名未注册或解析失败返回 null。首次解析后缓存结果。</summary>
    PackedScene? Scene(string? assetId);
}
