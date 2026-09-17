namespace DungeonChessBattle.Game.GamePanels;

/// <summary>mod 行发出的启停请求。</summary>
/// <param name="ModId">mod ID。</param>
/// <param name="Enabled">请求切换到的启停状态。</param>
public sealed record ModToggleRequest(string ModId, bool Enabled);