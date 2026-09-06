using Godot;

namespace DungeonChessBattle.Game.Shared;

/// <summary>
/// 展示层注册面：注册什么资源、注册成什么名字由内容方自定义，宿主只把本接口递过去。
/// 与数据面 <c>IModRuntime</c> 同构——内置先注册，后注册的同名条目覆盖前者，因此 mod 天然改写内置展示。
/// 资源名是全局命名空间，跨 mod 引用即用他包注册的名字；纹理与场景以取供器登记，
/// 首次查询时才执行，令「先引用后注册」的包次序不影响解析结果。
/// </summary>
public interface IModDisplayRuntime {
    /// <summary>注册纹理资源，同 id 覆盖。供器只在首次查询时被调用一次。</summary>
    void RegisterTexture(string id, Func<Texture2D?> provider);

    /// <summary>注册场景模板资源，同 id 覆盖。供器只在首次查询时被调用一次。</summary>
    void RegisterScene(string id, Func<PackedScene?> provider);

    /// <summary>注册技能展示视图，同 Id 覆盖。</summary>
    void RegisterSkill(ISkillView view);

    /// <summary>注册 Buff 展示视图，同 BuffTypeId 覆盖；类型 ID 为 0 的视图不参与注册。</summary>
    void RegisterBuff(IBuffView view);

    /// <summary>注册单位展示视图，同 ConfigKey 覆盖。</summary>
    void RegisterUnit(IUnitView view);

    /// <summary>注册副本展示视图，同 Key 覆盖。</summary>
    void RegisterDungeon(IDungeonView view);
}
