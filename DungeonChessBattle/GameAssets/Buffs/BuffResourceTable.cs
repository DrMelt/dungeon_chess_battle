using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// Buff 资源强类型映射表（基于 .tres 资源文件 + BuffTypeId 匹配）。
///
/// 在 Godot 编辑器中通过 [Export] 拖拽所有 Buff .tres 资源到 BuffResources 数组。
/// 运行时通过每个资源的 BuffTypeId（来自 BuffDefinition.BuffTypeId）自动构建反向查找字典，
/// 供 Buff 图标、名称与描述展示使用，无需任何字符串 ID。
/// </summary>
[GlobalClass]
public partial class BuffResourceTable : Resource {
    private static BuffResourceTable Instance {
        get {
            if (field != null)
                return field;

            field = GD.Load<BuffResourceTable>(
                "res://GameAssets/Buffs/res_buff_resource_table.tres");
            field.Initialize();
            return field;
        }
    }

    /// <summary>在 Godot 编辑器中拖拽的全部 Buff 资源。</summary>
    [Export]
    public Godot.Collections.Array<BuffBaseGodot> BuffResources { get; set; } = [];

    /// <summary>运行时查找字典：BuffTypeId → Buff 资源副本。</summary>
    private readonly System.Collections.Generic.Dictionary<ushort, BuffBaseGodot> _lookup = [];
    private bool _initialized;

    /// <summary>
    /// 初始化查找字典。每个 Buff 资源的 BuffTypeId 来自其 Config.BuffTypeId，
    /// 因此可以作为 Key 精准匹配。
    /// </summary>
    private void Initialize() {
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

    /// <summary>
    /// 通过 Buff 类型 ID 查找对应的 Buff 资源实例。
    /// </summary>
    /// <param name="buffTypeId">Buff 配置 ID。</param>
    /// <returns>Buff 资源新副本；未注册返回 null。</returns>
    public static BuffBaseGodot? GetResourceByBuffTypeId(ushort buffTypeId) {
        var table = Instance; // 触发懒加载
        if (table._lookup.TryGetValue(buffTypeId, out var template))
            return (BuffBaseGodot)template.Duplicate();
        return null;
    }
}
