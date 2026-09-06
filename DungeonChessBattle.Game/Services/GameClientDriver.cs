using Godot;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 客户端网络驱动节点：在 Godot 渲染主线程每帧驱动 GameClientService 的
/// 网络轮询与 LES 实体更新。与参考项目（LiteEntitySystemUnityExample）一致，
/// 由主线程同一帧串行执行 PollEvents + EntityManager.Update。
/// 使网络状态与实体事件先于输入采集/提交。
/// 同时监听连接状态：断开即通知回放浏览服务清空在途，旧会话凭证已作废。
/// </summary>
public partial class GameClientDriver : Node {
    private bool _subscribed;

    /// <summary>节点就绪：订阅连接状态事件。</summary>
    public override void _Ready() {
        // 连接丢失/主动断开都会触发 ConnectionChanged(false)，此时旧会话凭证已作废
        ServiceLocator.ClientService.ConnectionChanged += OnConnectionChanged;
        _subscribed = true;
    }

    /// <summary>节点退出：取消订阅，避免单例服务上的事件泄漏。</summary>
    public override void _ExitTree() {
        if (!_subscribed)
            return;
        ServiceLocator.ClientService.ConnectionChanged -= OnConnectionChanged;
        _subscribed = false;
    }

    /// <summary>连接断开：通知回放浏览服务取消在途并清空过程状态。</summary>
    private static void OnConnectionChanged(string host, int port, bool connected) {
        if (!connected)
            ServiceLocator.ReplayService.OnSessionInvalid();
    }

    /// <summary>每帧驱动客户端服务的网络轮询与实体更新。</summary>
    public override void _Process(double delta) {
        ServiceLocator.ClientService.Update((float)delta);
    }
}
