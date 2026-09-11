using System.Collections.Generic;
using DungeonChessBattle.Game.Services;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = Battle.Shared.Content.DungeonConfig;

/// <summary>
/// 副本资源强类型映射表（运行时构造 + 类型驱动匹配）。
/// 表不依赖任何 <c>res://</c> 资源文件：内容全部来自 mod，条目由 <c>ModAssetsMapper</c>
/// 以 <see cref="ModDungeonResource"/> 运行时构造并注册。以 Config（内容注册表中的静态副本定义实例）
/// 为键构建反查字典，供环境场景模板实例化；副本显示名与描述经 <c>ServiceLocator.ModAssets</c> 展示索引取。
/// 表实例由 ResourceTables 组合根构造，本类不持有加载入口。
/// </summary>
[GlobalClass]
public partial class DungeonResourceTable : Resource {
    /// <summary>运行时查找字典：DungeonConfig → 副本资源。</summary>
    private readonly Dictionary<DungeonConfigDef, DungeonResourceBaseGodot> _lookup = [];

    /// <summary>追加运行时 mod 副本资源；同 Config 覆盖已有条目。</summary>
    internal void RegisterModResource(DungeonResourceBaseGodot resource) {
        if (resource.InternalConfig is { } config)
            _lookup[config] = resource;
    }

    /// <summary>已注册的全部副本资源，均由 <c>ModAssetsMapper</c> 运行时构造。</summary>
    public IReadOnlyCollection<DungeonResourceBaseGodot> AllResources => _lookup.Values;

    /// <summary>该副本定义是否已有展示资源；有则以其为模板改写 mod 声明了的字段。</summary>
    internal bool TryGetResource(DungeonConfigDef config, out DungeonResourceBaseGodot? resource) {
        bool found = _lookup.TryGetValue(config, out var template);
        resource = template;
        return found;
    }

    /// <summary>按副本键取副本资源；副本未注册或资源未映射时返回 null。</summary>
    private DungeonResourceBaseGodot? GetResource(string? dungeonKey) {
        var config = ServiceLocator.GameContent.Registry.GetDungeon(dungeonKey);
        if (config != null && _lookup.TryGetValue(config, out var res))
            return res;
        return null;
    }

    /// <summary>
    /// 按副本键实例化环境表现场景；副本未注册或资源未配置环境场景返回 null。
    /// 副本键由权威侧同步，未同步或未注册时不为环境对象造场景，由调用方在键到达后重试。
    /// 主题已在场景模板内固化，加载即成品。
    /// 返回实例未挂载，由消费方 AddChild 使用。
    /// </summary>
    public Node3D? InstantiateEnvironment(string? dungeonKey) {
        var resource = GetResource(dungeonKey);
        return resource?.EnvScene?.Instantiate<Node3D>();
    }
}
