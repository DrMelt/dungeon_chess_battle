using DungeonChessBattle.Client.Battle;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 战斗输入控制器：负责采集玩家输入（移动/瞄准/技能）并提交到战斗服务。
/// 由 MainScene 在每帧 _Process 中调度 Tick；UI 阻塞（等待目标选择）时暂缓采集。
/// </summary>
public partial class BattleInputController : Node {
    /// <summary>3D 交互桥接（仅读取 IsBlockingInput 判断 UI 是否阻塞输入）。</summary>
    [Export]
    private PlayerOperationInterfaceInfo? _playerOperationInterfaceInfo;

    private Vector2 _moveDir;
    private byte _skillFlags;
    private Vector2 _aimPos;

    /// <summary>
    /// 每帧采集输入并提交到战斗服务。
    /// </summary>
    /// <param name="service">当前战斗服务。</param>
    public void Tick(IClientBattleService service) {
        // UI 阻塞时跳过战斗输入收集（等待技能/移动目标选择中）
        if (_playerOperationInterfaceInfo?.IsBlockingInput == true)
            return;

        _moveDir = Input.GetVector("Move_Left", "Move_Right", "Move_Up", "Move_Down");

        _skillFlags = 0;

        var mousePos = GetViewport().GetMousePosition();
        _aimPos = new Vector2(mousePos.X, mousePos.Y);

        service.SubmitPlayerInput(_moveDir.X, _moveDir.Y, _skillFlags, _aimPos.X, _aimPos.Y);
    }

    /// <summary>退出战斗时清零输入缓冲。</summary>
    public void Reset() {
        _moveDir = Vector2.Zero;
        _skillFlags = 0;
        _aimPos = Vector2.Zero;
    }
}
