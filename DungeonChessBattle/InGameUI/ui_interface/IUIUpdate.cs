namespace DungeonChessBattle.InGameUI.ui_interface;

/// <summary>
/// UI 更新接口，实现此接口的组件可通过单位状态刷新显示。
/// </summary>
public interface IUIUpdate {
    /// <summary>
    /// 根据单位状态更新 UI 显示。
    /// </summary>
    /// <param name="unitShow">目标单位状态。</param>
    void UpdateUI_WithUnit(UnitState unitShow);
}
