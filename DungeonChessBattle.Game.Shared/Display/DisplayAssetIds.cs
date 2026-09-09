namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 宿主登记的展示资源名：引擎预置场景在展示注册表里的键。
/// mod 展示代码以此名引用宿主对象，不必硬编码字符串；<c>res://</c> 路径映射留在主工程
/// <c>BuiltinDisplayAssets</c>，本类只是名，不含路径。
/// </summary>
public static class DisplayAssetIds {
    /// <summary>矩形范围伤害施放特效。</summary>
    public const string RectRangeDamage = "apply_effect_rect_range_damage";

    /// <summary>矩形范围提示。</summary>
    public const string RangeHintRect = "range_hint_rect";

    /// <summary>圆形区域提示。</summary>
    public const string RangeHintCircular = "range_hint_circular";

    /// <summary>默认林地环境。</summary>
    public const string EnvForest = "env_forest";

    /// <summary>深邃洞窟环境。</summary>
    public const string EnvCave = "env_cave";
}
