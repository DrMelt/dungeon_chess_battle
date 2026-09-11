using System;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 技能效果提示协调器：按技能资源携带的范围提示场景创建、挂载与销毁选目标预览。
/// 场景模板归属技能资源（RangeHintScene），实例能否被驱动由是否实现 <see cref="IRectRangeHint"/> 判定，
/// 本节点只负责实例生命周期；实例初始化延迟到挂载后一帧，保证作用场景的 _Ready 已完成。
/// </summary>
public partial class EffectHints : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<EffectHints> _logger = ServiceLocator.GetLogger<EffectHints>();

    /// <summary>当前显示的范围提示实例。</summary>
    public Node3D? ActiveHint {
        get; private set;
    }

    /// <summary>挂载后待执行的初始化回调，_Process 首帧执行。</summary>
    private Action? _pendingInit;

    /// <summary>
    /// 按技能资源创建并挂载范围提示；已有提示先销毁。
    /// 模板未配置或场景根不是 Node3D 时不创建，不实现契约的场景即刻回收并记错误。
    /// </summary>
    /// <param name="skill">技能资源（持有 RangeHintScene 模板）。</param>
    /// <param name="init">实例挂载且 _Ready 完成后执行的初始化回调。</param>
    /// <returns>创建的范围提示实例；未创建返回 null。</returns>
    public IRectRangeHint? ShowRangeHint(UnitSkillBaseGodot skill, Action<IRectRangeHint> init) {
        HideRangeHint();
        if (skill.RangeHintScene?.Instantiate() is not Node3D hint)
            return null;
        if (hint is not IRectRangeHint rangeHint) {
            _logger.LogError("范围提示场景根未实现 IRectRangeHint，已回收：{ScenePath}", hint.SceneFilePath);
            hint.QueueFree();
            return null;
        }

        AddChild(hint);
        ActiveHint = hint;
        _pendingInit = () => init(rangeHint);
        return rangeHint;
    }

    /// <summary>销毁当前范围提示并取消待执行的初始化。</summary>
    public void HideRangeHint() {
        _pendingInit = null;
        if (ActiveHint == null)
            return;
        ActiveHint.QueueFree();
        ActiveHint = null;
    }

    /// <summary>每帧执行待处理的初始化回调。</summary>
    public override void _Process(double delta) {
        if (_pendingInit == null)
            return;
        var init = _pendingInit;
        _pendingInit = null;
        init();
    }
}
