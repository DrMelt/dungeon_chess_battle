using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// Godot 技能资源。绑定内容注册表中的领域技能定义，承载展示所需数据（图标/名称/描述/范围提示场景），
/// 施法/冷却由服务端权威结算，客户端仅据 Pawn 同步数据渲染。
/// 产出 <see cref="SkillDisplay"/>，与 mod 技能展示数据同经 <c>ServiceLocator.ModAssets</c> 查询。
/// 只由 <c>ModAssetsMapper</c> 装配期构造、Godot 不实例化本类，故不入编辑器资源表；
/// 资源表交出本类实例本体，装配完成后一律只读。
/// </summary>
public partial class UnitSkillBaseGodot : Resource {
    /// <summary>本资源承载的领域技能定义，装配期注入后不变。</summary>
    internal SkillDefinition Config { get; }

    /// <remarks>显示名先回退到技能键，mod 声明展示数据后由 <see cref="ApplyViewData"/> 覆盖。</remarks>
    internal UnitSkillBaseGodot(SkillDefinition config) {
        Config = config;
        ApplyViewData(null, config.SkillId.Id, null, null);
    }

    /// <summary>技能强类型 ID（来自 SkillDefinition.SkillId，用于按 Pawn.SkillCasting 匹配）。</summary>
    public SkillKeyId SkillId => Config.SkillId;

    /// <summary>产出注册表用的展示数据。</summary>
    internal SkillDisplay ToDisplay() =>
        new(SkillId, SkillName, SkillDescription, Icon, RangeHintScene);

    /// <summary>技能图标。</summary>
    [Export]
    public Texture2D? Icon { get; private set; } = null;

    /// <summary>技能名称。</summary>
    [Export]
    public string SkillName { get; private set; } = "";

    /// <summary>技能描述（支持多行文本）。</summary>
    [Export(PropertyHint.MultilineText)]
    public string SkillDescription { get; private set; } = "";

    /// <summary>选择位置目标时展示的范围提示场景模板。</summary>
    [Export]
    public PackedScene? RangeHintScene {
        get; private set;
    }

    /// <summary>
    /// 由 mod 资源装配运行时填充展示字段；null 或空串的成员保持原值，内部调用。
    /// </summary>
    internal void ApplyViewData(
        Texture2D? icon, string? name, string? description, PackedScene? rangeHintScene) {
        if (icon is not null)
            Icon = icon;
        if (!string.IsNullOrEmpty(name))
            SkillName = name;
        if (!string.IsNullOrEmpty(description))
            SkillDescription = description;
        if (rangeHintScene is not null)
            RangeHintScene = rangeHintScene;
    }

    /// <summary>技能施放总时长（秒）。</summary>
    public float SkillSpellTime => Config.SpellTime;

    /// <summary>是否需要指定单位目标。</summary>
    public bool NeedUnitTarget => Config.NeedUnitTarget;

    /// <summary>是否需要指定位置目标。</summary>
    public bool NeedPosTarget => Config.NeedPosTarget;

    /// <summary>技能可释放的目标类型，直接读 SkillDefinition.TargetPolicy，UI 目标选择与展示读取。</summary>
    public SkillTargetPolicy TargetPolicy => Config.TargetPolicy;
}
