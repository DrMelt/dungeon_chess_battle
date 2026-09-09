using System.Collections.Generic;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Buff 资源强类型映射表（运行时构造 + BuffTypeId 匹配）。
/// 表不依赖任何 <c>res://</c> 资源文件：内容全部来自 mod，条目由 <c>ModAssetsMapper</c>
/// 以 <see cref="ModBuffResource"/> 运行时构造并注册。以 BuffTypeId 为键构建反查字典，
/// 供 Buff 图标、名称与描述展示使用，无需任何字符串 ID。
/// 表实例由 ResourceTables 组合根构造，本类不持有加载入口。
/// </summary>
[GlobalClass]
public partial class BuffResourceTable : Resource {
    /// <summary>运行时查找字典：BuffTypeId → Buff 资源。</summary>
    private readonly Dictionary<ushort, BuffBaseGodot> _lookup = [];

    /// <summary>追加运行时 mod Buff 资源；同 BuffTypeId 覆盖已有条目。</summary>
    internal void RegisterModResource(BuffBaseGodot resource) {
        if (resource.BuffTypeId != 0)
            _lookup[resource.BuffTypeId] = resource;
    }

    /// <summary>已注册的全部 Buff 资源，均由 <c>ModAssetsMapper</c> 运行时构造。</summary>
    public IReadOnlyCollection<BuffBaseGodot> AllResources => _lookup.Values;

    /// <summary>该 BuffTypeId 是否已有展示资源；有则以其为模板改写 mod 声明了的字段。</summary>
    internal bool TryGetResource(ushort buffTypeId, out BuffBaseGodot? resource) {
        bool found = _lookup.TryGetValue(buffTypeId, out var template);
        resource = template;
        return found;
    }
}
