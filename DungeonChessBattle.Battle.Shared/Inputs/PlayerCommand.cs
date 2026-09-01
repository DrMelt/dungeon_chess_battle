using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared.Inputs;

/// <summary>玩家命令类型，决定 <see cref="PlayerCommand"/> 的生效字段。</summary>
public enum PlayerCommandKind : byte {
    /// <summary>本帧移动意图，零向量即静止，随 <c>BattleScene.Tick</c> 末作废。</summary>
    Move,

    /// <summary>施法请求，经排队器接管后转投为本帧施法意图。</summary>
    Cast,

    /// <summary>聚焦目标设定，持续状态，不参与逐帧作废。</summary>
    Focus,
}

/// <summary>
/// 玩家命令：一次玩家输入的扁平形态，键一律为网络 ID，在线提交、回放录制与回放注入共用同一形状。
/// 只声明形状不做合法性判定，生效字段由 <see cref="Kind"/> 决定。
/// </summary>
public readonly record struct PlayerCommand {
    /// <summary>命令来源单位的网络 ID。</summary>
    public required ushort NetId {
        get; init;
    }

    /// <summary>命令类型。</summary>
    public required PlayerCommandKind Kind {
        get; init;
    }

    /// <summary>移动方向，仅 <see cref="PlayerCommandKind.Move"/> 生效。</summary>
    public Vector2 MoveDir {
        get; init;
    }

    /// <summary>技能配置键，仅 <see cref="PlayerCommandKind.Cast"/> 生效。</summary>
    public string? SkillKey {
        get; init;
    }

    /// <summary>目标单位网络 ID：<c>Cast</c> 为 0 时走位置目标，<c>Focus</c> 为 0 时清除聚焦。</summary>
    public ushort TargetNetId {
        get; init;
    }

    /// <summary>位置目标 X，仅 <c>Cast</c> 且 <see cref="TargetNetId"/> 为 0 时生效。</summary>
    public float TargetPosX {
        get; init;
    }

    /// <summary>位置目标 Z，仅 <c>Cast</c> 且 <see cref="TargetNetId"/> 为 0 时生效。</summary>
    public float TargetPosZ {
        get; init;
    }

    /// <summary>移动命令：方向取输入的 X/Y 两分量，零向量即静止。</summary>
    public static PlayerCommand Move(ushort netId, float moveX, float moveY) => new() {
        NetId = netId,
        Kind = PlayerCommandKind.Move,
        MoveDir = new Vector2(moveX, moveY),
    };

    /// <summary>施法命令：目标与锚点按原样承载，取舍在 <see cref="CastTargetPos"/>。</summary>
    public static PlayerCommand Cast(ushort netId, string skillKey, ushort targetNetId, float targetX, float targetZ) => new() {
        NetId = netId,
        Kind = PlayerCommandKind.Cast,
        SkillKey = skillKey,
        TargetNetId = targetNetId,
        TargetPosX = targetX,
        TargetPosZ = targetZ,
    };

    /// <summary>聚焦命令。</summary>
    public static PlayerCommand Focus(ushort netId, ushort targetNetId) => new() {
        NetId = netId,
        Kind = PlayerCommandKind.Focus,
        TargetNetId = targetNetId,
    };

    /// <summary>施法位置锚点：单位目标丢弃锚点，位置目标取 XZ 平面坐标。</summary>
    public Vector2? CastTargetPos => TargetNetId != 0 ? null : new Vector2(TargetPosX, TargetPosZ);
}
