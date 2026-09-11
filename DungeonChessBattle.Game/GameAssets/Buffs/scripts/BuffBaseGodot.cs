using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Godot Buff 基类资源，承载 BuffDefinition 引用与展示属性（图标/名称/描述）。
/// 产出 <see cref="BuffDisplay"/>，与 mod Buff 展示数据同经 <c>ServiceLocator.ModAssets</c> 查询。
/// </summary>
[GlobalClass]
public partial class BuffBaseGodot : Resource {
    /// <summary>
    /// 子类重写此属性，直接返回内容注册表中的领域 Buff 定义（类型安全，编译期检查）。
    /// </summary>
    protected virtual BuffDefinition? Config => null;

    /// <summary>Buff 图标。</summary>
    [Export]
    public Texture2D? Icon {
        get; private set;
    }

    /// <summary>Buff 键，与内容注册表里的 Buff 身份对齐；未绑定定义为无键。</summary>
    public BuffTypeId BuffTypeId => Config?.BuffTypeId ?? BuffTypeId.None;

    /// <summary>产出注册表用的展示数据。</summary>
    internal BuffDisplay ToDisplay() => new(BuffTypeId, BuffName, BuffDescription, Icon);

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
            BuffName = name;
        if (!string.IsNullOrEmpty(description))
            BuffDescription = description;
    }
}
