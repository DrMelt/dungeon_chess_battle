namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 客户端展示资源表组合根：技能/Buff/副本三张资源表的唯一实例持有者。
/// 引擎不再内置展示条目：表从空构造，条目由 <c>ModAssets</c> 展示装配以运行时 Mod*Resource 填充。
/// 与 ServiceLocator 同属静态组合根，维持项目无 DI 容器约定。
/// </summary>
public static class ResourceTables {
    private static SkillResourceTable? _skills;
    private static BuffResourceTable? _buffs;
    private static DungeonResourceTable? _dungeons;

    /// <summary>技能资源表单例。初始为空表，经 <c>ModAssetsMapper</c> 装配填充。</summary>
    public static SkillResourceTable Skills => _skills ??= new SkillResourceTable();

    /// <summary>Buff 资源表单例。初始为空表，经 <c>ModAssetsMapper</c> 装配填充。</summary>
    public static BuffResourceTable Buffs => _buffs ??= new BuffResourceTable();

    /// <summary>副本资源表单例。初始为空表，经 <c>ModAssetsMapper</c> 装配填充。</summary>
    public static DungeonResourceTable Dungeons => _dungeons ??= new DungeonResourceTable();
}
