using DungeonChessBattle.Battle.Shared.ValueObjects;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = Battle.Config.Shared.Content.DungeonConfig;
using DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// Godot 副本资源。绑定内容注册表中的领域副本定义，承载展示所需数据（环境场景/显示名/描述），
/// 敌人生成与战场布局由服务端依据共享配置权威结算，客户端据此映射展示。
/// 产出 <see cref="DungeonDisplay"/>，与 mod 副本展示数据同经 <c>ServiceLocator.ModAssets</c> 查询。
/// 只由 <c>ModAssetsMapper</c> 装配期构造、Godot 不实例化本类，故不入编辑器资源表；
/// 资源表交出本类实例本体，装配完成后一律只读。
/// </summary>
public partial class DungeonResourceBaseGodot : Resource {
    /// <summary>本资源承载的领域副本定义，装配期注入后不变。</summary>
    internal DungeonConfigDef Config { get; }

    /// <remarks>显示名先回退到副本键，mod 声明展示数据后由 <see cref="ApplyViewData"/> 覆盖。</remarks>
    internal DungeonResourceBaseGodot(DungeonConfigDef config) {
        Config = config;
        ApplyViewData(null, config.DungeonKey.Value, null);
    }

    /// <summary>副本键，来自领域配置。</summary>
    public DungeonKeyId DungeonKey => Config.DungeonKey;

    /// <summary>产出注册表用的展示数据。</summary>
    internal DungeonDisplay ToDisplay() => new(DungeonKey, DisplayName, Description, EnvScene);

    /// <summary>环境表现场景模板，主题已在场景内固化，未配置为 null。</summary>
    [Export]
    public PackedScene? EnvScene {
        get; private set;
    }

    /// <summary>副本显示名。</summary>
    [Export]
    public string DisplayName { get; private set; } = "";

    /// <summary>副本描述（支持多行文本）。</summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; private set; } = "";

    /// <summary>由 mod 资源装配运行时填充展示字段；null 或空串的成员保持原值，内部调用。</summary>
    internal void ApplyViewData(
        PackedScene? envScene, string? displayName, string? description) {
        if (envScene is not null)
            EnvScene = envScene;
        if (!string.IsNullOrEmpty(displayName))
            DisplayName = displayName;
        if (!string.IsNullOrEmpty(description))
            Description = description;
    }
}
