using DungeonChessBattle.Client.Battle.Diagnostics;
using DungeonChessBattle.Services;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 网络状态调试覆盖层：纯 View，只消费 <see cref="NetworkStatusSnapshot"/> DTO，
/// 每帧从房间客户端读取并格式化显示。按 <see cref="ToggleKey"/> 切换显隐。
/// 未连接房间时显示 Disconnected；进入战斗后追加 LES 实体同步段。
/// </summary>
public partial class NetworkDebugOverlay : Label {
    /// <summary>显隐切换键（集中在常量，便于调整）。</summary>
    private const Key ToggleKey = Key.F3;

    public override void _Process(double delta) {
        Text = FormatStatus(ServiceLocator.ClientService.RoomClient.NetworkStatus);
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (@event is InputEventKey { Pressed: true, PhysicalKeycode: ToggleKey, Echo: false })
            Visible = !Visible;
    }

    /// <summary>把网络状态快照格式化为多行文本。</summary>
    /// <param name="s">网络状态快照。</param>
    private static string FormatStatus(NetworkStatusSnapshot s) {
        if (!s.IsConnected)
            return "Disconnected";

        var t = s.Transport;
        var lines = new System.Collections.Generic.List<string>
        {
            $"[NET] {s.Host}:{s.Port}",
            $"Ping: {t.LatencyMs} ms",
            $"IN:  {t.BytesInPerSecond / 1000f:0.0} KB/s ({t.PacketsInPerSecond})",
            $"OUT: {t.BytesOutPerSecond / 1000f:0.0} KB/s ({t.PacketsOutPerSecond})",
        };

        if (s.Entity is { } e) {
            lines.Add("-- LES --");
            lines.Add($"ServerTick:  {e.ServerTick}");
            lines.Add($"Tick:        {e.Tick}");
            lines.Add($"LastProcess: {e.LastProcessedTick}");
            lines.Add($"StoredCmds:  {e.StoredCommands}");
            lines.Add($"Entities:    {e.EntitiesCount}");
            lines.Add($"ServerInput: {e.ServerInputBuffer}");
            lines.Add($"LerpBufCnt:  {e.LerpBufferCount}");
            lines.Add($"LerpBufTime: {e.LerpBufferTimeLength:0.000}");
            lines.Add($"Jitter:      {e.NetworkJitter:0.0}");
            lines.Add($"PendingRem:  {e.PendingToRemoveEntities}");
        }

        return string.Join('\n', lines);
    }
}
