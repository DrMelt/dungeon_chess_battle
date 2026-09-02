using System.Collections.Generic;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = GameConfig.Models.DungeonConfig;

/// <summary>
/// 副本资源强类型映射表（基于 .tres 资源文件 + 类型驱动匹配）。
/// 在 Godot 编辑器中通过 [Export] 拖拽所有副本 .tres 资源到 DungeonResources 数组，
/// 运行时通过每个资源的 Config 属性（返回 GameConfigDB 中的唯一静态副本定义实例）
/// 自动构建查找字典，客户端据副本键映射显示名与描述，无需任何字符串 ID。
/// 表实例由 ResourceTables 组合根加载并调用 Initialize，本类不持有加载入口。
/// 新增副本时只需在 res_dungeon_resource_table.tres 中拖入对应的 .tres 资源即可。
/// </summary>
[GlobalClass]
public partial class DungeonResourceTable : Resource {
    /// <summary>在 Godot 编辑器中拖拽的全部副本资源。</summary>
    [Export]
    public Godot.Collections.Array<DungeonResourceBaseGodot> DungeonResources { get; set; } = [];

    /// <summary>运行时查找字典：DungeonConfig → 副本资源。</summary>
    private readonly Dictionary<DungeonConfigDef, DungeonResourceBaseGodot> _lookup = [];
    private bool _initialized;

    /// <summary>初始化查找字典。每个副本资源的 Config 属性返回 GameConfigDB 中的唯一静态实例。由 ResourceTables 加载后调用，幂等。</summary>
    internal void Initialize() {
        if (_initialized)
            return;

        foreach (var res in DungeonResources) {
            var config = res.InternalConfig;
            if (config != null)
                _lookup[config] = res;
        }

        _initialized = true;
    }

    /// <summary>
    /// 按副本键获取副本资源；副本未注册或资源未在资源表映射时返回 null。
    /// 环境主题、显示名等展示数据统一经此入口装配。
    /// </summary>
    public DungeonResourceBaseGodot? GetResource(string? dungeonKey) {
        Initialize();
        var config = DungeonRegistry.Instance.GetByKey(dungeonKey);
        if (config != null && _lookup.TryGetValue(config, out var res))
            return res;
        return null;
    }

    /// <summary>按副本键获取客户端显示名；副本或资源未注册返回 null。</summary>
    public string? GetDisplayName(string? dungeonKey) => GetResource(dungeonKey)?.DisplayName;

    /// <summary>按副本键获取客户端描述；副本或资源未注册返回 null。</summary>
    public string? GetDescription(string? dungeonKey) => GetResource(dungeonKey)?.Description;

    /// <summary>
    /// 按副本键实例化环境表现场景；副本未注册或资源未配置环境场景返回 null。
    /// 副本键未同步/未注册时回退默认副本模板，保证环境对象始终可实例化，
    /// 主题仍由 DungeonEnv.ApplyDungeonTheme 按会话真实键修正。
    /// 返回实例未挂载，由消费方 AddChild 并调用 ApplyDungeonTheme 装配主题。
    /// </summary>
    public DungeonEnv? InstantiateEnvironment(string? dungeonKey) {
        // 副本键未同步/未注册时回退默认副本，保证环境对象始终可实例化
        var resource = GetResource(dungeonKey)
            ?? GetResource(GameConfigDB.DefaultDungeonKey);
        return resource?.EnvScene?.Instantiate<DungeonEnv>();
    }
}
