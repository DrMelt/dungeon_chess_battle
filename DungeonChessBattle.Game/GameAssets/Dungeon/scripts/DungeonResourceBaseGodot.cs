using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = DungeonChessBattle.Battle.Shared.Content.DungeonConfig;
using DungeonChessBattle.Game.Shared;

/// <summary>
/// Godot 副本资源基类。仅承载展示所需数据（显示名/描述）与领域副本定义引用，
/// 敌人生成与战场布局由服务端依据共享配置权威结算，客户端据此映射展示。
/// 实现 <see cref="IDungeonView"/>，与 mod 副本视图同经 <c>ModAssets</c> 查询。
/// </summary>
[GlobalClass]
public abstract partial class DungeonResourceBaseGodot : Resource, IDungeonView {
    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的领域副本定义（类型安全，编译期检查）。
    /// </summary>
    protected virtual DungeonConfigDef? Config => null;

    /// <summary>内部访问 Config，供 DungeonResourceTable 等程序集内部使用。</summary>
    internal DungeonConfigDef? InternalConfig => Config;

    /// <summary>副本键，来自领域配置。</summary>
    public string DungeonKey => Config?.DungeonKey ?? "";

    /// <summary>IDungeonView 以 Key 暴露副本键。</summary>
    string IDungeonView.Key => DungeonKey;

    /// <summary>环境主题：地面颜色。未配置时回退默认林地主题。</summary>
    [Export]
    public Color GroundColor { get; private set; } = new(0.28f, 0.38f, 0.24f, 1f);

    /// <summary>环境主题：天空背景颜色。未配置时回退默认林地主题。</summary>
    [Export]
    public Color SkyColor { get; private set; } = new(0.60f, 0.78f, 0.72f, 1f);

    /// <summary>环境主题：方向光补光颜色。未配置时回退默认林地主题。</summary>
    [Export]
    public Color LightColor { get; private set; } = new(1.00f, 0.95f, 0.85f, 1f);

    /// <summary>环境表现场景模板，未配置为 null。</summary>
    [Export]
    public PackedScene? EnvScene {
        get; private set;
    }

    // IDungeonView 用可空表达「未声明」，本类成员总有值，在此对齐
    Color? IDungeonView.GroundColor => GroundColor;
    Color? IDungeonView.SkyColor => SkyColor;
    Color? IDungeonView.LightColor => LightColor;

    /// <summary>副本显示名。</summary>
    [Export]
    public string DisplayName { get; private set; } = "";

    /// <summary>副本描述（支持多行文本）。</summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; private set; } = "";

    /// <summary>由 mod 资源装配运行时填充展示字段；null 或空串的成员保持模板原值，内部调用。</summary>
    internal void ApplyViewData(
        Color? groundColor, Color? skyColor, Color? lightColor,
        PackedScene? envScene, string? displayName, string? description) {
        if (groundColor is { } ground)
            GroundColor = ground;
        if (skyColor is { } sky)
            SkyColor = sky;
        if (lightColor is { } light)
            LightColor = light;
        if (envScene is not null)
            EnvScene = envScene;
        if (!string.IsNullOrEmpty(displayName))
            DisplayName = displayName!;
        if (!string.IsNullOrEmpty(description))
            Description = description!;
    }
}
