using System.Numerics;

namespace DungeonChessBattle.Battle.Domain.Movement;

/// <summary>
/// 战斗世界与移动执行器的衔接：位置回读与移动输入输出。
/// 在线端实现为 LES 实体（位置读实体 SyncVar、输入写实体移动输入）；回放离线模式不注入，位置由回放驱动直接结算。
/// </summary>
public interface IBattleMovementBridge {
    /// <summary>按网络 ID 读取单位当前权威位置，BattleScene 每帧结算前回读。</summary>
    Vector2 GetPosition(ushort netId);

    /// <summary>按网络 ID 输出单位本帧移动输入，由外部移动执行器消费。</summary>
    void SetMoveInput(ushort netId, Vector2 moveDirection);
}
