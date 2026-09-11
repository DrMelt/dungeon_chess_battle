using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// 展示注册表口：场景资源的注册与查询在本接口，条目展示数据的读也在这里、写经 <see cref="IModDisplayRuntime"/>。
/// 场景名是全局命名空间，跨 mod 引用即用他包注册的名字，同名后注册覆盖先注册；场景以取供器登记，
/// 首次查询时才执行，令「先引用后注册」的包次序不影响解析结果。
/// 图标纹理不经本口：它随条目数据以对象直接携带，来源可以是包内 <c>.tres</c> 的内联引用，无须注册名。
/// 引擎预置场景名与各 mod 注册进来的展示数据汇在同一张表里，mod 与宿主 UI 查同一张表。
/// 条目数据同键后注册者覆盖先注册者，注册次序由装配方保证；装配期读到的内容取决于已完成的注册，
/// 宿主在引擎侧注册之后才把本口递给 mod。
/// </summary>
public interface IDisplayRegistry {
    /// <summary>注册场景模板资源，同 id 覆盖。供器只在首次查询时被调用一次。</summary>
    void RegisterScene(string id, Func<PackedScene?> provider);

    /// <summary>按技能键取展示数据；未注册返回 null。</summary>
    SkillDisplay? GetSkill(string skillKey);

    /// <summary>按 Buff 键取展示数据；未注册返回 null。</summary>
    BuffDisplay? GetBuff(string buffKey);

    /// <summary>按副本键取展示数据；键为空或未注册返回 null。</summary>
    DungeonDisplay? GetDungeon(string? dungeonKey);

    /// <summary>按单位配置键取展示数据；未注册返回 null。</summary>
    UnitDisplay? GetUnit(string configKey);

    /// <summary>按场景名取场景模板；名未注册或解析失败返回 null。首次解析后缓存结果。</summary>
    PackedScene? Scene(string? assetId);
}
