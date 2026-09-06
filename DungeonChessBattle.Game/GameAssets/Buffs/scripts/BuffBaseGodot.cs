using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Godot Buff 基类资源，承载 BuffDefinition 引用与展示属性（图标/名称/描述）。
/// 实现 <see cref="IBuffView"/>，与 mod Buff 视图同经 <c>ModAssets</c> 查询。
/// </summary>
[GlobalClass]
public partial class BuffBaseGodot : Resource, IBuffView {
    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的领域 Buff 定义。
    /// </summary>
    protected virtual BuffDefinition? Config => null;

    /// <summary>Buff 图标。</summary>
    [Export]
    public Texture2D? Icon {
        get; private set;
    }

    /// <summary>Buff 全局唯一 ID（对应配置表与 SyncBuffData.BuffTypeId）。</summary>
    public ushort BuffTypeId => Config?.BuffTypeId ?? 0;

    // IBuffView 用通用成员名，本类成员带 Buff 前缀，在此对齐
    string IBuffView.Name => BuffName;
    string IBuffView.Description => BuffDescription;

    /// <summary>Buff 名称。</summary>
    [Export]
    public string BuffName {
        get; private set;
    } = "";

    /// <summary>Buff 描述。</summary>
    [Export]
    public string BuffDescription {
        get; private set;
    } = "";

    /// <summary>由 mod 资源装配运行时填充展示字段；null 或空串的成员保持模板原值，内部调用。</summary>
    internal void ApplyViewData(Texture2D? icon, string? name, string? description) {
        if (icon is not null)
            Icon = icon;
        if (!string.IsNullOrEmpty(name))
            BuffName = name!;
        if (!string.IsNullOrEmpty(description))
            BuffDescription = description!;
    }
}
