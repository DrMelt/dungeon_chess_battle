using System;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 客户端展示资源表组合根：技能/Buff/副本三张资源表的唯一加载入口。
/// 所有 .tres 资源表的 res:// 路径只在本类出现，消费方经静态属性获取表实例，不触碰路径字符串。
/// 与 ServiceLocator 同属静态组合根，维持项目无 DI 容器约定。
/// </summary>
public static class ResourceTables {
    private static SkillResourceTable? _skills;
    private static BuffResourceTable? _buffs;
    private static DungeonResourceTable? _dungeons;

    // Godot res:// 虚拟资源路径，非文件系统绝对路径，S1075 误报
#pragma warning disable S1075
    private const string SkillsTableRes = "res://GameAssets/Skills/res_skill_resource_table.tres";
    private const string BuffsTableRes = "res://GameAssets/Buffs/res_buff_resource_table.tres";
    private const string DungeonsTableRes = "res://GameAssets/Dungeon/res_dungeon_resource_table.tres";
#pragma warning restore S1075

    /// <summary>技能资源表单例，懒加载并初始化反查字典。</summary>
    public static SkillResourceTable Skills => _skills ??= LoadAndInit<SkillResourceTable>(
        SkillsTableRes,
        static table => table.Initialize());

    /// <summary>Buff 资源表单例，懒加载并初始化反查字典。</summary>
    public static BuffResourceTable Buffs => _buffs ??= LoadAndInit<BuffResourceTable>(
        BuffsTableRes,
        static table => table.Initialize());

    /// <summary>副本资源表单例，懒加载并初始化反查字典。</summary>
    public static DungeonResourceTable Dungeons => _dungeons ??= LoadAndInit<DungeonResourceTable>(
        DungeonsTableRes,
        static table => table.Initialize());

    private static T LoadAndInit<T>(string path, Action<T> init) where T : Resource {
        var table = GD.Load<T>(path);
        init(table);
        return table;
    }
}
