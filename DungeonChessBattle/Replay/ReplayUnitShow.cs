using DungeonChessBattle.Client.Replay;
using Godot;

namespace DungeonChessBattle.Replay;

/// <summary>
/// 回放单位展示节点：绑定 <see cref="ReplayUnitView"/>，每帧直读位置/朝向驱动网格。
/// 回放是纯观赏，数据源为本地回放引擎展示模型，不依赖网络实体。
/// </summary>
public partial class ReplayUnitShow : Node3D {
    private ReplayUnitView? _view;

    /// <summary>绑定的回放展示模型，由 ReplayCoordinator 注入。</summary>
    public ReplayUnitView? View {
        get => _view;
        set {
            _view = value;
            Visible = value != null;
        }
    }

    /// <summary>每帧从回放展示模型直读位置与朝向。</summary>
    public override void _Process(double delta) {
        var view = _view;
        if (view == null)
            return;

        GlobalPosition = new Vector3(view.Position.X, 0f, view.Position.Y);
        Visible = !view.IsDead;

        var dir = view.Direction;
        if (dir.LengthSquared() > 0.0001f)
            LookAt(GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
    }
}
