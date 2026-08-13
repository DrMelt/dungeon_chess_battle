using DungeonChessBattle.Battle.Domain.Combat;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// Godot Buff 基类资源，承载 BuffDefinition 引用与展示属性（图标/名称/描述）。
/// </summary>
[GlobalClass]
public partial class BuffBaseGodot : Resource {
    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的领域 Buff 定义（类型安全，编译期检查）。
    /// </summary>
    protected virtual BuffDefinition? Config => null;

    /// <summary>Buff 图标。</summary>
    [Export]
    public Texture2D? icon;

    /// <summary>Buff 全局唯一 ID（对应配置表与 SyncBuffData.BuffTypeId）。</summary>
    public ushort BuffTypeId => Config?.BuffTypeId ?? 0;

    /// <summary>Buff 名称。</summary>
    [Export]
    public string BuffName { get; private set; } = "";
    /// <summary>Buff 描述。</summary>
    [Export]
    public string BuffDescription { get; private set; } = "";
    /// <summary>图标资源路径。</summary>
    public string IconPath => icon?.ResourcePath ?? "";
}
