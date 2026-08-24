using LiteNetLib.Utils;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 服务器到客户端的可靠事件日志消息，经传输层 ReliableOrdered 通道送达。
/// 连接内可靠有序，断线重连期间的消息不补发。
/// </summary>
public struct ReliableBattleEventLog : INetSerializable {
    /// <summary>单帧事件数量上限，防止畸形帧一次性分配超大数组。</summary>
    public const int MaxEventsPerFrame = 4096;

    /// <summary>整帧战斗事件日志，编码结构与 BattleEventCoder 一致。</summary>
    public SyncBattleEvent[] Events;

    /// <inheritdoc />
    public readonly void Serialize(NetDataWriter writer) {
        writer.Put(Events.Length);
        foreach (var e in Events) {
            writer.Put(e.Type);
            writer.Put(e.A);
            writer.Put(e.B);
            writer.Put(e.C);
            writer.Put(e.Value);
            writer.Put(e.SkillKey);
        }
    }

    /// <inheritdoc />
    public void Deserialize(NetDataReader reader) {
        int count = reader.GetInt();
        if (count < 0 || count > MaxEventsPerFrame)
            throw new InvalidDataException($"ReliableBattleEventLog events count out of range: {count}.");
        var events = new SyncBattleEvent[count];
        for (int i = 0; i < events.Length; i++) {
            events[i] = new SyncBattleEvent {
                Type = reader.GetByte(),
                A = reader.GetUShort(),
                B = reader.GetUShort(),
                C = reader.GetUShort(),
                Value = reader.GetFloat(),
                SkillKey = reader.GetString(),
            };
        }
        Events = events;
    }
}
