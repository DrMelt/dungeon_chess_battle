using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Buff 资源强类型映射表（运行时构造 + Buff 键匹配）。
/// 表不依赖任何 <c>res://</c> 资源文件：内容全部来自 mod，条目由 <c>ModAssetsMapper</c>
/// 以 <see cref="BuffBaseGodot"/> 运行时构造并注册。以 Buff 键为键构建反查字典，
/// 供 Buff 图标、名称与描述展示使用。表交出资源本体而非副本，条目装配完成后只读。
/// 表实例由 ResourceTables 组合根构造，本类不持有加载入口。
/// </summary>
[GlobalClass]
public partial class BuffResourceTable : Resource {
    /// <summary>运行时查找字典：Buff 键 → Buff 资源。</summary>
    private readonly Dictionary<BuffTypeId, BuffBaseGodot> _lookup = [];

    /// <summary>追加运行时 mod Buff 资源；同键覆盖已有条目。</summary>
    internal void RegisterModResource(BuffBaseGodot resource) =>
        _lookup[resource.BuffTypeId] = resource;

    /// <summary>已注册的全部 Buff 资源，均由 <c>ModAssetsMapper</c> 运行时构造。</summary>
    public IReadOnlyCollection<BuffBaseGodot> AllResources => _lookup.Values;
}
