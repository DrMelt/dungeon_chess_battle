using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// Godot 技能基类资源。仅承载展示所需数据（图标/名称/描述）与配置 ID，
/// 施法/冷却由服务端权威结算，客户端仅据 Pawn 同步数据渲染。
/// </summary>
[GlobalClass]
public partial class UnitSkillBaseGodot : Resource {
    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的 SkillConfig（类型安全，编译期检查）。
    /// </summary>
    protected virtual SkillConfig? Config => null;

    /// <summary>
    /// 内部访问 Config，供 SkillResourceTable 等程序集内部使用。
    /// </summary>
    internal SkillConfig? InternalConfig => Config;

    /// <summary>技能全局唯一 ID（来自 SkillConfig.Id，用于按 Pawn.SkillCasting 匹配）。</summary>
    public ushort SkillId => Config?.Id ?? 0;

    /// <summary>技能图标。</summary>
    [Export]
    public Texture2D? Icon { get; private set; } = null;

    /// <summary>技能名称。</summary>
    [Export]
    public string SkillName { get; private set; } = "";

    /// <summary>技能描述（支持多行文本）。</summary>
    [Export(PropertyHint.MultilineText)]
    public string SkillDescription { get; private set; } = "";

    /// <summary>技能施放总时长（秒）。</summary>
    public float SkillSpellTime => Config?.SkillSpellTime ?? 0;

    /// <summary>是否需要指定单位目标。</summary>
    public bool NeedUnitTarget => Config?.NeedUnitTarget ?? false;

    /// <summary>是否需要指定位置目标。</summary>
    public bool NeedPosTarget => Config?.NeedPosTarget ?? false;
}
