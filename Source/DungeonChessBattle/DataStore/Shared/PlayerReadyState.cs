namespace DungeonChessBattle.Server.DataStore.Shared;

/// <summary>
/// 房间内单个玩家的准备状态，只读快照项。
/// </summary>
/// <param name="PlayerName">玩家显示名。</param>
/// <param name="Ready">是否已准备。</param>
public sealed record PlayerReadyState(string PlayerName, bool Ready);
