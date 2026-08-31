using System.Collections.Generic;
using DungeonChessBattle.Battle.Client.Diagnostics;
using DungeonChessBattle.Game.Services;
using Godot;

namespace DungeonChessBattle.Debug;

/// <summary>
/// 网络状态调试覆盖层：纯 View，只消费 <see cref="NetworkStatusSnapshot"/> DTO，
/// 每帧从房间客户端读取并格式化显示，不含任何指标计算。按 <see cref="ToggleKey"/> 切换显隐。
/// 未连接房间时显示 Disconnected；进入战斗后追加 LES 实体同步段。
/// </summary>
public partial class NetworkDebugOverlay : Label {
    /// <summary>显隐切换键（集中在常量，便于调整）。</summary>
    private const Key ToggleKey = Key.F3;

    /// <summary>每帧刷新网络状态文本。</summary>
    public override void _Process(double delta) {
        Text = FormatStatus(ServiceLocator.ClientService.RoomNetworkStatus);
    }

    /// <summary>处理显隐切换快捷键。</summary>
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
        var lines = new List<string>
        {
            $"[NET] {s.Host}:{s.Port}",
            $"RTT:      {t.RttMs} ms (one-way {t.OneWayMs})",
            $"IN:  {t.BytesInPerSecond / 1000f:0.0} KB/s ({t.PacketsInPerSecond})",
            $"OUT: {t.BytesOutPerSecond / 1000f:0.0} KB/s ({t.PacketsOutPerSecond})",
            $"Loss: {t.PacketLossPercent}% since connect | OutRelQ: {t.OutgoingReliableQueue}",
        };

        if (s.Entity is { } e) {
            lines.Add("-- LES --");
            lines.Add(e.TickLagTrusted
                ? $"TickLag: {e.AckLagTicks} t ({e.AckLagMs:0} ms) = up {e.UplinkTicks} + queue {e.ServerQueueTicks}"
                : "TickLag: n/a, waiting for diff state");
            lines.Add($"  net {e.NetAckLagTicks} t ({e.NetAckLagMs:0} ms) + debt {e.PlaybackDebtTicks} t");
            lines.Add($"Tick width:  {e.TickMs:0.0} ms | state every {e.StateSendIntervalTicks} t");
            lines.Add($"LocalTick:   {e.LocalTick} | ack {e.SrvAckTick} | recv {e.SrvRecvTick}");
            lines.Add($"ServerTick:  {e.ServerTick} | state A/B {e.SrvStateTickA}/{e.SrvStateTickB}");
            lines.Add($"Spread:      {e.StateSpreadTicks} t | last 1s avg {e.StateSpreadAvg:0.00} max {e.StateSpreadMax}");
            lines.Add($"StoredCmds:  {e.StoredCommands} | SrvInputBuf {e.ServerInputBuffer}");
            lines.Add($"LerpBuf:     {e.LerpBufferCount} ({e.LerpBufferTimeSeconds:0.000} s)");
            lines.Add($"Jitter:      max {e.JitterMaxSeconds:0.000} avg {e.JitterAvgSeconds:0.000} s");
            lines.Add($"Entities:    {e.EntitiesCount} | PendingRem {e.PendingToRemoveEntities}");
            lines.Add($"StateSize:   {e.StateSize} B");
        }

        return string.Join('\n', lines);
    }
}
