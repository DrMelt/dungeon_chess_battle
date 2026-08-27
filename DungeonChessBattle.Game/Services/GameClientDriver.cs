using Godot;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 客户端网络驱动节点：在 Godot 渲染主线程每帧驱动 GameClientService 的
/// 网络轮询与 LES 实体更新。与参考项目（LiteEntitySystemUnityExample）一致，
/// 由主线程同一帧串行执行 PollEvents + EntityManager.Update。
/// 使网络状态与实体事件先于输入采集/提交。
/// </summary>
public partial class GameClientDriver : Node {

    /// <summary>每帧驱动客户端服务的网络轮询与实体更新。</summary>
    public override void _Process(double delta) {
        ServiceLocator.ClientService.Update((float)delta);
    }
}
