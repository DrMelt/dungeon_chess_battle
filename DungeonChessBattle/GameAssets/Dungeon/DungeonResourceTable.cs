using System;
using System.Collections.Generic;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.GameAssets;

using DungeonConfigDef = DungeonChessBattle.GameConfig.Models.DungeonConfig;

/// <summary>
/// 副本资源强类型映射表（基于 .tres 资源文件 + 类型驱动匹配）。
/// 在 Godot 编辑器中通过 [Export] 拖拽所有副本 .tres 资源到 DungeonResources 数组，
/// 运行时通过每个资源的 Config 属性（返回 GameConfigDB 中的唯一静态副本定义实例）
/// 自动构建查找字典，客户端据副本键映射显示名与描述，无需任何字符串 ID。
/// 新增副本时只需在 res_dungeon_resource_table.tres 中拖入对应的 .tres 资源即可。
/// </summary>
[GlobalClass]
public partial class DungeonResourceTable : Resource {
    /// <summary>全局懒加载单例。</summary>
    private static DungeonResourceTable Instance {
        get {
            if (field != null)
                return field;

            field = GD.Load<DungeonResourceTable>(
                "res://GameAssets/Dungeon/res_dungeon_resource_table.tres");
            field.Initialize();
            return field;
        }
    }

    /// <summary>在 Godot 编辑器中拖拽的全部副本资源。</summary>
    [Export]
    public Godot.Collections.Array<DungeonResourceBaseGodot> DungeonResources { get; set; } = [];

    /// <summary>运行时查找字典：DungeonConfig → 副本资源。</summary>
    private readonly Dictionary<DungeonConfigDef, DungeonResourceBaseGodot> _lookup = [];
    private bool _initialized;

    /// <summary>初始化查找字典。每个副本资源的 Config 属性返回 GameConfigDB 中的唯一静态实例。</summary>
    private void Initialize() {
        if (_initialized)
            return;

        foreach (var res in DungeonResources) {
            var config = res.InternalConfig;
            if (config != null)
                _lookup[config] = res;
        }

        _initialized = true;
    }

    /// <summary>按副本键获取客户端显示名；副本或资源未注册返回 null。</summary>
    public static string? GetDisplayName(string? dungeonKey) {
        var config = DungeonRegistry.Instance.GetByKey(dungeonKey);
        if (config != null && Instance._lookup.TryGetValue(config, out var res))
            return res.DisplayName;
        return null;
    }

    /// <summary>按副本键获取客户端描述；副本或资源未注册返回 null。</summary>
    public static string? GetDescription(string? dungeonKey) {
        var config = DungeonRegistry.Instance.GetByKey(dungeonKey);
        if (config != null && Instance._lookup.TryGetValue(config, out var res))
            return res.Description;
        return null;
    }

    /// <summary>
    /// 启动时自检：验证所有已注册副本都在资源表中有映射，未注册即启动失败。
    /// 在游戏启动时调用一次，效果等同于编译期检查。
    /// </summary>
    public static void Validate() {
        var table = Instance;
        foreach (var dungeon in DungeonRegistry.Instance.All) {
            if (!table._lookup.ContainsKey(dungeon))
                throw new InvalidOperationException(
                    $"自检失败：副本 '{dungeon.DungeonKey}' 未在 res_dungeon_resource_table.tres 的 DungeonResources 中注册。");
        }
    }
}
