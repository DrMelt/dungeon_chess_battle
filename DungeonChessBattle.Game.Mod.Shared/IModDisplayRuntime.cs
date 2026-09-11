using DungeonChessBattle.Game.Shared.Display;

namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// 条目展示数据注册面：注册什么条目、注册成什么键由内容方自定义，宿主只把本接口递过去。
/// 与数据面 <c>IModRuntime</c> 同构——后注册的同名条目覆盖前者，因此后装载的 mod 天然改写先装载的展示。
/// 覆盖是字段级的：数据里声明了什么就改什么，未声明字段沿用被覆盖者。
/// 场景资源不经本接口，注册与查询都在 <see cref="IDisplayRegistry"/>；图标纹理随条目数据以对象携带。
/// </summary>
public interface IModDisplayRuntime {
    /// <summary>注册技能展示数据，同 Id 覆盖。</summary>
    void RegisterSkill(SkillDisplay display);

    /// <summary>注册 Buff 展示数据，同 BuffTypeId 覆盖；键为空串的数据不参与注册。</summary>
    void RegisterBuff(BuffDisplay display);

    /// <summary>注册单位展示数据，同 ConfigKey 覆盖。</summary>
    void RegisterUnit(UnitDisplay display);

    /// <summary>注册副本展示数据，同 Key 覆盖。</summary>
    void RegisterDungeon(DungeonDisplay display);
}
