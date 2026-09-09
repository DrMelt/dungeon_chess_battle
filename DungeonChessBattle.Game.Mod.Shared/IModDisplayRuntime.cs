using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// 展示注册器写面：注册什么资源、注册成什么名字由内容方自定义，宿主只把本接口递过去。
/// 与数据面 <c>IModRuntime</c> 同构——后注册的同名条目覆盖前者，因此后装载的 mod 天然改写先装载的展示。
/// 覆盖是字段级的：数据里声明了什么就改什么，未声明字段沿用被覆盖者。
/// 资源名是全局命名空间，跨 mod 引用即用他包注册的名字；纹理与场景以取供器登记，
/// 首次查询时才执行，令「先引用后注册」的包次序不影响解析结果。
/// </summary>
public interface IModDisplayRuntime {
    /// <summary>注册纹理资源，同 id 覆盖。供器只在首次查询时被调用一次。</summary>
    void RegisterTexture(string id, Func<Texture2D?> provider);

    /// <summary>注册场景模板资源，同 id 覆盖。供器只在首次查询时被调用一次。</summary>
    void RegisterScene(string id, Func<PackedScene?> provider);

    /// <summary>注册技能展示数据，同 Id 覆盖。</summary>
    void RegisterSkill(SkillDisplay display);

    /// <summary>注册 Buff 展示数据，同 BuffTypeId 覆盖；类型 ID 为 0 的数据不参与注册。</summary>
    void RegisterBuff(BuffDisplay display);

    /// <summary>注册单位展示数据，同 ConfigKey 覆盖。</summary>
    void RegisterUnit(UnitDisplay display);

    /// <summary>注册副本展示数据，同 Key 覆盖。</summary>
    void RegisterDungeon(DungeonDisplay display);
}
