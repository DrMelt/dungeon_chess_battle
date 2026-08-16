namespace DungeonChessBattle.Protocol;

/// <summary>
/// SignalR Hub 方法名常量，客户端与服务端共用，消除硬编码字符串。
/// </summary>
public static class HubMethods {
    // ─── 客户端到服务端 InvokeAsync ───

    /// <summary>创建房间。</summary>
    public const string CreateRoom = "CreateRoom";

    /// <summary>加入房间。</summary>
    public const string JoinRoom = "JoinRoom";

    /// <summary>获取招募板房间列表。</summary>
    public const string ListRooms = "ListRooms";

    /// <summary>准备阶段：添加单位。</summary>
    public const string AddPrepareUnit = "AddPrepareUnit";

    /// <summary>准备阶段：移除单位。</summary>
    public const string RemovePrepareUnit = "RemovePrepareUnit";

    /// <summary>准备阶段：开始战斗。</summary>
    public const string StartBattle = "StartBattle";

    /// <summary>准备阶段：设置是否已准备。</summary>
    public const string SetReady = "SetReady";

    /// <summary>重连房间。</summary>
    public const string ReconnectRoom = "ReconnectRoom";

    /// <summary>离开房间，准备阶段主动退出。</summary>
    public const string LeaveRoom = "LeaveRoom";

    // ─── 服务端到客户端广播回调 ───

    /// <summary>准备阶段战斗启动重定向。</summary>
    public const string OnPrepareBattleRedirect = "OnPrepareBattleRedirect";

    /// <summary>房间完整状态快照广播，服务端组装单发。</summary>
    public const string OnRoomSnapshot = "OnRoomSnapshot";
}
