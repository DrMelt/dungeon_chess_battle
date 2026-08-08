using DungeonChessBattle.Entities;

namespace DungeonChessBattle.InGameUI.ui_interface;

/// <summary>
/// UI 更新接口，实现此接口的组件可直接通过 UnitPawn（网络同步 SyncVar）刷新显示。
/// </summary>
public interface IUIUpdate {
    /// <summary>
    /// 根据单位 Pawn 更新 UI 显示。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn（客户端只读 SyncVar）。</param>
    void UpdateUI_WithUnit(UnitPawn pawn);
}
