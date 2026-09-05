using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Buff 资源强类型映射表（基于 .tres 资源文件 + BuffTypeId 匹配）。
///
/// 在 Godot 编辑器中通过 [Export] 拖拽所有 Buff .tres 资源到 BuffResources 数组。
/// 运行时通过每个资源的 BuffTypeId（来自 BuffDefinition.BuffTypeId）自动构建反向查找字典，
/// 供 Buff 图标、名称与描述展示使用，无需任何字符串 ID。
/// 表实例由 ResourceTables 组合根加载并调用 Initialize，本类不持有加载入口。
/// </summary>
[GlobalClass]
public partial class BuffResourceTable : Resource {
    /// <summary>在 Godot 编辑器中拖拽的全部 Buff 资源。</summary>
    [Export]
    public Godot.Collections.Array<BuffBaseGodot> BuffResources { get; set; } = [];

    /// <summary>运行时查找字典：BuffTypeId → Buff 资源。</summary>
    private readonly System.Collections.Generic.Dictionary<ushort, BuffBaseGodot> _lookup = [];
    private bool _initialized;

    /// <summary>
    /// 初始化查找字典。每个 Buff 资源的 BuffTypeId 来自其 Config.BuffTypeId，
    /// 因此可以作为 Key 精准匹配。由 ResourceTables 加载后调用，幂等。
    /// </summary>
    internal void Initialize() {
        if (_initialized)
            return;

        foreach (var res in BuffResources) {
            if (res == null)
                continue;
            // 直接以原始资源为模板（只读访问 Config，不修改原始资源）
            var id = res.BuffTypeId;
            if (id != 0)
                _lookup[id] = res;
        }

        _initialized = true;
    }

    /// <summary>追加运行时 mod Buff 资源；同 BuffTypeId 覆盖已有条目。必须在 Initialize 后调用。</summary>
    internal void RegisterModResource(BuffBaseGodot resource) {
        if (resource.BuffTypeId != 0)
            _lookup[resource.BuffTypeId] = resource;
    }

    /// <summary>
    /// 通过 Buff 类型 ID 查找对应的 Buff 资源实例。
    /// </summary>
    /// <param name="buffTypeId">Buff 配置 ID。</param>
    /// <returns>Buff 资源实例；未注册返回 null。</returns>
    public BuffBaseGodot? GetResourceByBuffTypeId(ushort buffTypeId) {
        Initialize();
        return _lookup.TryGetValue(buffTypeId, out var template) ? template : null;
    }
}
