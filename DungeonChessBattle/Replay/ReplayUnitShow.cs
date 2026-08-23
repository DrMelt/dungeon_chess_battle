using DungeonChessBattle.Replay;
using Godot;

namespace DungeonChessBattle.ReplayUI;

/// <summary>
/// 回放单位展示节点：绑定回放引擎与单位网络 ID，每帧直读战斗世界 BattleUnit 驱动网格。
/// 回放是纯观赏，数据源为本地回放引擎战斗世界，不依赖网络实体。
/// </summary>
public partial class ReplayUnitShow : Node3D {
    private ReplayEngine? _engine;
    private ushort _netId;

    /// <summary>绑定回放引擎与单位网络 ID，由 ReplayCoordinator 注入；引擎重置后自动跟随新单位实例。</summary>
    public void Bind(ReplayEngine engine, ushort netId) {
        _engine = engine;
        _netId = netId;
    }

    /// <summary>每帧从回放引擎战斗世界直读位置与朝向。</summary>
    public override void _Process(double delta) {
        var unit = _engine?.FindUnit(_netId);
        if (unit == null) {
            Visible = false;
            return;
        }

        GlobalPosition = new Vector3(unit.Position.X, 0f, unit.Position.Y);
        Visible = unit.Health > 0f;

        var dir = unit.Direction;
        if (dir.LengthSquared() > 0.0001f)
            LookAt(GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
    }
}
