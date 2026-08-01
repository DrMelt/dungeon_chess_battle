using Godot;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责初始化服务、连接 BattleStarted 信号，将战斗启动委托给 BattlePanel。
/// </summary>
public partial class MainScene : Node {
    [Export]
    private MainMenu? _mainMenu;

    [Export]
    private GameLobby? _gameLobby;

    [Export]
    private BattlePanel? _battlePanel;

    public override void _Ready() {
        // 验证导出引用
        ValidateExports();

        // 连接 GameLobby 的 BattleStarted 信号 → BattlePanel
        if (_gameLobby != null && _battlePanel != null) {
            _gameLobby.BattleStarted += (roomId) => {
                GD.Print($"[MainScene] BattleStarted signal received for room: {roomId}");

                // 委托给 BattlePanel 初始化战斗
                _battlePanel.EnterBattle(roomId, ServiceLocator.ClientService.RoomClient);

                // 隐藏大厅 UI
                _gameLobby.Visible = false;
            };

            // 监听战斗结束，返回大厅
            _battlePanel.BattleEnded += () => {
                GD.Print("[MainScene] Battle ended, returning to lobby.");
                _gameLobby.Visible = true;
            };
        }

        // 默认显示主菜单
        _mainMenu?.OpenPanelFrom();

        GD.Print("[MainScene] Initialized.");
    }

    private void ValidateExports() {
        if (_mainMenu == null)
            GD.PrintErr("[MainScene] [Export] _mainMenu is not assigned!");
        if (_gameLobby == null)
            GD.PrintErr("[MainScene] [Export] _gameLobby is not assigned!");
        if (_battlePanel == null)
            GD.PrintErr("[MainScene] [Export] _battlePanel is not assigned!");
    }
}
