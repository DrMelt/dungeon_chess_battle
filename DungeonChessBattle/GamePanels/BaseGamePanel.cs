using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 游戏面板基类，提供面板间导航功能。
/// 打开时记录来源面板引用，关闭时自动返回来源面板。
/// </summary>
public partial class BaseGamePanel : Control {
    private BaseGamePanel? _caller;

    /// <summary>
    /// 面板被打开后调用。子类可重写以执行自定义逻辑。
    /// </summary>
    protected virtual void OnPanelOpened() {
    }

    /// <summary>
    /// 面板被关闭后调用。子类可重写以执行自定义逻辑。
    /// </summary>
    protected virtual void OnPanelClosed() {
    }

    /// <summary>
    /// 从指定来源面板打开当前面板。
    /// 隐藏来源面板，显示自身，并记录来源引用以便后续返回。
    /// </summary>
    /// <param name="caller">调用来源面板。关闭当前面板时将返回到该面板。</param>
    public void OpenPanelFrom(BaseGamePanel? caller = null) {
        _caller = caller;
        caller?.Visible = false;
        Visible = true;
        OnPanelOpened();
    }

    /// <summary>
    /// 关闭当前面板，返回到来源面板。
    /// </summary>
    public void ClosePanel() {
        OnPanelClosed();
        Visible = false;
        _caller?.OpenPanelFrom();
    }

    /// <summary>
    /// 返回到来源面板。保留 caller 的原有导航上下文，不覆盖其 _caller。
    /// 与 ClosePanel 的区别：GoBack 直接显示 caller，不调用 OpenPanelFrom() 从而避免重置 caller 的 _caller。
    /// </summary>
    public void GoBack() {
        OnPanelClosed();
        Visible = false;
        if (_caller != null) {
            _caller.Visible = true;
            _caller.OnPanelOpened();
        }
    }

    /// <summary>
    /// 从当前面板导航到目标面板。隐藏自身，显示目标面板。
    /// 各面板按钮打开其他面板时统一通过此方法。
    /// </summary>
    /// <param name="target">目标面板实例</param>
    protected void NavigateTo(BaseGamePanel? target) {
        if (target == null) {
            GD.PrintErr($"[{GetType().Name}] NavigateTo failed: target panel is not assigned.");
            return;
        }
        var logger = ServiceLocator.CreateLogger(GetType().Name);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Navigate: {From} -> {To}", GetType().Name, target.GetType().Name);
        target.OpenPanelFrom(this);
    }
}
