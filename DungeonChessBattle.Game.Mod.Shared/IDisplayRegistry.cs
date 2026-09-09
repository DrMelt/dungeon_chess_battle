using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// 展示注册器读面：引擎预置资源名与各 mod 注册进来的展示数据汇在同一张表里，消费方按身份键取数据。
/// 写经 <see cref="IModDisplayRuntime"/>，本接口只读；注册次序由装配方保证，同键后注册者覆盖先注册者。
/// 装配期读到的内容取决于已完成的注册，宿主在引擎侧注册之后才把本口递给 mod。
/// </summary>
public interface IDisplayRegistry {
    /// <summary>按技能键取展示数据；未注册返回 null。</summary>
    SkillDisplay? GetSkill(string skillKey);

    /// <summary>按 BuffTypeId 取展示数据；未注册返回 null。</summary>
    BuffDisplay? GetBuff(ushort buffTypeId);

    /// <summary>按副本键取展示数据；键为空或未注册返回 null。</summary>
    DungeonDisplay? GetDungeon(string? dungeonKey);

    /// <summary>按单位配置键取展示数据；未注册返回 null。</summary>
    UnitDisplay? GetUnit(string configKey);

    /// <summary>按资源名取纹理；名未注册或解析失败返回 null。首次解析后缓存结果。</summary>
    Texture2D? Texture(string? assetId);

    /// <summary>按资源名取场景模板；名未注册或解析失败返回 null。首次解析后缓存结果。</summary>
    PackedScene? Scene(string? assetId);
}
