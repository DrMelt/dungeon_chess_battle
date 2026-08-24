using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using DamageType = DungeonChessBattle.Battle.Domain.Combat.DamageType;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 受击伤害提示浮字，按伤害类型着色并带淡出效果。
/// </summary>
public partial class TookDamageInfo : FadeInfo {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<TookDamageInfo> _logger = ServiceLocator.GetLogger<TookDamageInfo>();


    /// <summary>伤害数值标签。</summary>
    [ExportGroup("Internal")]
    [Export]
    private Label? damageLabel;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        base._Ready();
        if (damageLabel == null)
            _logger.LogError("damageLabel is not assigned!");
    }

    /// <summary>
    /// 初始化伤害提示：按伤害类型设置颜色并显示数值。
    /// </summary>
    /// <param name="damage">伤害数值。</param>
    /// <param name="type">伤害类型。</param>
    /// <param name="playerUISettings">玩家 UI 设置，提供伤害颜色配置。</param>
    public void Init(float damage, DamageType type, PlayerUISettings playerUISettings) {
        if (damageLabel == null)
            return;
        if (type == DamageType.Magic) {
            damageLabel.SelfModulate = playerUISettings.MagicInfoColor;
        }
        else if (type == DamageType.Physical) {
            damageLabel.SelfModulate = playerUISettings.PhysicalInfoColor;
        }
        damageLabel.Text = damage.ToString("F0");
    }

    /// <summary>
    /// 初始化数值提示：直接指定文本颜色，供治疗等非伤害表现复用。
    /// </summary>
    /// <param name="value">展示数值。</param>
    /// <param name="color">文本颜色。</param>
    public void Init(float value, Color color) {
        if (damageLabel == null)
            return;
        damageLabel.SelfModulate = color;
        damageLabel.Text = value.ToString("F0");
    }

    /// <summary>
    /// 每帧更新淡出动画。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        UpdateFade(delta);
    }
}
