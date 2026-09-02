using DungeonChessBattle.Battle.Shared.Combat;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Godot 技能基类资源。仅承载展示所需数据（图标/名称/描述）与技能定义引用，
/// 施法/冷却由服务端权威结算，客户端仅据 Pawn 同步数据渲染。
/// </summary>
[GlobalClass]
public partial class UnitSkillBaseGodot : Resource {
    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的领域技能定义（类型安全，编译期检查）。
    /// </summary>
    protected virtual SkillDefinition? Config => null;

    /// <summary>
    /// 内部访问 Config，供 SkillResourceTable 等程序集内部使用。
    /// </summary>
    internal SkillDefinition? InternalConfig => Config;

    /// <summary>技能强类型 ID（来自 SkillDefinition.SkillId，用于按 Pawn.SkillCasting 匹配）。</summary>
    public SkillKeyId SkillId => Config?.SkillId ?? default;

    /// <summary>技能图标。</summary>
    [Export]
    public Texture2D? Icon { get; private set; } = null;

    /// <summary>技能名称。</summary>
    [Export]
    public string SkillName { get; private set; } = "";

    /// <summary>技能描述（支持多行文本）。</summary>
    [Export(PropertyHint.MultilineText)]
    public string SkillDescription { get; private set; } = "";

    /// <summary>技能施放时在目标位置实例化的特效场景模板。</summary>
    [Export]
    public PackedScene? ApplyEffectScene {
        get; private set;
    }

    /// <summary>选择位置目标时展示的范围提示场景模板。</summary>
    [Export]
    public PackedScene? RangeHintScene {
        get; private set;
    }

    /// <summary>
    /// 实例化施放特效节点；模板未配置返回 null。
    /// </summary>
    public Node3D? CreateApplyEffect() => ApplyEffectScene?.Instantiate<Node3D>();

    /// <summary>
    /// 实例化范围提示节点；模板未配置返回 null。
    /// </summary>
    public Node3D? CreateRangeHint() => RangeHintScene?.Instantiate<Node3D>();

    /// <summary>技能施放总时长（秒）。</summary>
    public float SkillSpellTime => Config?.SpellTime ?? 0;

    /// <summary>是否需要指定单位目标。</summary>
    public bool NeedUnitTarget => Config?.NeedUnitTarget ?? false;

    /// <summary>是否需要指定位置目标。</summary>
    public bool NeedPosTarget => Config?.NeedPosTarget ?? false;

    /// <summary>技能可释放的目标类型，直接读 SkillDefinition.TargetPolicy，UI 目标选择与展示读取。</summary>
    public SkillTargetPolicy TargetPolicy => Config?.TargetPolicy ?? SkillTargetPolicy.None;
}
