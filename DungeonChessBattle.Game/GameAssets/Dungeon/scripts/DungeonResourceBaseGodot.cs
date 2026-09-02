using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = GameConfig.Models.DungeonConfig;

/// <summary>
/// Godot 副本资源基类。仅承载展示所需数据（显示名/描述）与领域副本定义引用，
/// 敌人生成与战场布局由服务端依据共享配置权威结算，客户端据此映射展示。
/// </summary>
[GlobalClass]
public abstract partial class DungeonResourceBaseGodot : Resource {
    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的领域副本定义（类型安全，编译期检查）。
    /// </summary>
    protected virtual DungeonConfigDef? Config => null;

    /// <summary>内部访问 Config，供 DungeonResourceTable 等程序集内部使用。</summary>
    internal DungeonConfigDef? InternalConfig => Config;

    /// <summary>副本键，来自领域配置。</summary>
    public string DungeonKey => Config?.DungeonKey ?? "";

    /// <summary>环境主题：地面颜色。未配置时回退默认林地主题。</summary>
    [Export]
    public Color GroundColor { get; private set; } = new(0.28f, 0.38f, 0.24f, 1f);

    /// <summary>环境主题：天空背景颜色。未配置时回退默认林地主题。</summary>
    [Export]
    public Color SkyColor { get; private set; } = new(0.60f, 0.78f, 0.72f, 1f);

    /// <summary>环境主题：方向光补光颜色。未配置时回退默认林地主题。</summary>
    [Export]
    public Color LightColor { get; private set; } = new(1.00f, 0.95f, 0.85f, 1f);

    /// <summary>该副本的环境表现场景模板，由 DungeonResourceTable.InstantiateEnvironment 实例化。</summary>
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
}
