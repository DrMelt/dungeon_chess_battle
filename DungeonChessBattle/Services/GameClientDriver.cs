using Godot;

namespace DungeonChessBattle.Services;

/// <summary>
/// 客户端网络驱动节点：在 Godot 渲染主线程每帧驱动 GameClientService 的
/// 网络轮询与 LES 实体更新。与参考项目（LiteEntitySystemUnityExample）一致，
/// 由主线程同一帧串行执行 PollEvents + EntityManager.Update。
/// 使网络状态与实体事件先于输入采集/提交。
/// </summary>
public partial class GameClientDriver : Node {
    /// <summary>当前场景中的驱动节点实例（挂载后可用）。</summary>
    public static GameClientDriver? Instance {
        get; private set;
    }

    /// <summary>节点就绪：记录当前驱动节点实例。</summary>
    public override void _Ready() {
        Instance = this;
    }

    /// <summary>节点退出场景树：清理驱动节点实例引用。</summary>
    public override void _ExitTree() {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>每帧驱动客户端服务的网络轮询与实体更新。</summary>
    public override void _Process(double delta) {
        ServiceLocator.ClientService.Update((float)delta);
    }
}
