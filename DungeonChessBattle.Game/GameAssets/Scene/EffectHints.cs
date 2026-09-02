using System;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 技能效果提示协调器：按技能资源的范围提示场景创建、挂载与销毁选目标预览。
/// 场景模板归属技能资源（RangeHintScene），本节点只负责实例生命周期；
/// 实例初始化延迟到挂载后一帧，保证作用场景的 _Ready 已完成。
/// </summary>
public partial class EffectHints : Node {
    /// <summary>当前显示的范围提示实例。</summary>
    public Node3D? ActiveHint {
        get; private set;
    }

    /// <summary>挂载后待执行的初始化回调，_Process 首帧执行。</summary>
    private Action? _pendingInit;

    /// <summary>
    /// 按技能资源创建并挂载范围提示；已有提示先销毁。
    /// </summary>
    /// <typeparam name="T">范围提示具体类型。</typeparam>
    /// <param name="skill">技能资源（持有 RangeHintScene 模板）。</param>
    /// <param name="init">实例挂载且 _Ready 完成后执行的初始化回调。</param>
    /// <returns>创建的范围提示实例；模板未配置返回 null。</returns>
    public T? ShowRangeHint<T>(UnitSkillBaseGodot skill, Action<T> init) where T : Node3D {
        HideRangeHint();
        if (skill.CreateRangeHint() is not T hint)
            return null;

        AddChild(hint);
        ActiveHint = hint;
        _pendingInit = () => init((T)ActiveHint!);
        return hint;
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
