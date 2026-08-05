using DungeonChessBattle.Services;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 客户端网络驱动节点：在 Godot 渲染主线程每帧驱动 GameClientService 的
/// 网络轮询与 LES 实体更新。与参考项目（LiteEntitySystemUnityExample）一致，
/// 由主线程同一帧串行执行 PollEvents + EntityManager.Update。
/// 通过 ProcessPriority 保证在本场景其他节点的 _Process 之前执行，
/// 使网络状态与实体事件先于输入采集/提交。
/// </summary>
public partial class GameClientDriver : Node {
    /// <summary>当前场景中的驱动节点实例（挂载后可用）。</summary>
    public static GameClientDriver? Instance {
        get; private set;
    }

    public override void _Ready() {
        Instance = this;
        ProcessPriority = 100; // 高于 MainScene 的默认 0，确保先更新后输入
    }

    public override void _ExitTree() {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta) {
        ServiceLocator.ClientService.Update((float)delta);
    }
}
