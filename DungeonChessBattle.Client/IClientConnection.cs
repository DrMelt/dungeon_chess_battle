namespace DungeonChessBattle.Client;

/// <summary>
/// 客户端连接抽象（大厅或房间），供 GameClientService 统一管理连接生命周期。
/// 大厅（SignalR）与房间（LiteNetLib/LES）各自实现。
/// </summary>
public interface IClientConnection {
    /// <summary>是否已连接到服务端。</summary>
    bool IsConnected {
        get;
    }

    /// <summary>断开连接并清理状态。</summary>
    void Disconnect();

    /// <summary>每帧驱动网络轮询（SignalR 实现为空操作）。</summary>
    void Update(float delta);
}
