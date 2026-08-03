namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 轻量 Buff 事件数据结构，用于 IClientBattleService 接口事件传递。
/// 不含 LiteEntitySystem 序列化依赖，解耦 UI 层与 Entities 层。
/// </summary>
public struct BuffEventData {
    /// <summary>Buff 类型 ID</summary>
    public ushort BuffTypeId;

    /// <summary>剩余持续时间（秒）</summary>
    public float RemainingDuration;

    /// <summary>当前叠加层数</summary>
    public ushort StackCount;

    /// <summary>伤害类型（仅 DOT 有效）</summary>
    public byte DamageType;
}