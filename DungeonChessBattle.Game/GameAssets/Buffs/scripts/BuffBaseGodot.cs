using DungeonChessBattle.Battle.Config.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Godot Buff 资源，承载内容注册表中的领域 Buff 定义与展示属性（图标/名称/描述）。
/// 产出 <see cref="BuffDisplay"/>，与 mod Buff 展示数据同经 <c>ServiceLocator.ModAssets</c> 查询。
/// 只由 <c>ModAssetsMapper</c> 装配期构造、Godot 不实例化本类，故不入编辑器资源表；
/// 资源表交出本类实例本体，装配完成后一律只读。
/// </summary>
public partial class BuffBaseGodot : Resource {
    /// <summary>本资源承载的领域 Buff 定义，装配期注入后不变。</summary>
    internal BuffDefinition Config { get; }

    /// <remarks>显示名先回退到 Buff 键，mod 声明展示数据后由 <see cref="ApplyViewData"/> 覆盖。</remarks>
    internal BuffBaseGodot(BuffDefinition config) {
        Config = config;
        ApplyViewData(null, $"Buff {config.BuffTypeId.Value}", null);
    }

    /// <summary>Buff 图标。</summary>
    [Export]
    public Texture2D? Icon {
        get; private set;
    }

    /// <summary>Buff 键，与内容注册表里的 Buff 身份对齐。</summary>
    public BuffTypeId BuffTypeId => Config.BuffTypeId;

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

    /// <summary>由 mod 资源装配运行时填充展示字段；null 或空串的成员保持原值，内部调用。</summary>
    internal void ApplyViewData(Texture2D? icon, string? name, string? description) {
        if (icon is not null)
            Icon = icon;
        if (!string.IsNullOrEmpty(name))
            BuffName = name;
        if (!string.IsNullOrEmpty(description))
            BuffDescription = description;
    }
}
