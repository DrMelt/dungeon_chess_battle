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
/// 玩家命令：一次玩家输入的扁平形态，键一律为 <see cref="UnitId"/>，在线提交、回放录制与回放注入共用同一形状。
/// 只声明形状不做合法性判定，生效字段由 <see cref="Kind"/> 决定。
/// 本类型不落盘不上网：线上请求载荷与回放条目仍是原生 ushort，降级发生在构造点与 <c>ReplayCommands</c>。
/// </summary>
public readonly record struct PlayerCommand {
    /// <summary>命令来源单位：Move 为移动者、Cast 为施法者、Focus 为聚焦持有者。</summary>
    public required UnitId SourceUnitId {
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

    /// <summary>目标单位：<c>Cast</c> 为 <see cref="UnitId.None"/> 时走位置目标，<c>Focus</c> 为 <see cref="UnitId.None"/> 时清除聚焦。</summary>
    public UnitId TargetUnitId {
        get; init;
    }

    /// <summary>位置目标 X，仅 <c>Cast</c> 且 <see cref="TargetUnitId"/> 为无单位时生效。</summary>
    public float TargetPosX {
        get; init;
    }

    /// <summary>位置目标 Z，仅 <c>Cast</c> 且 <see cref="TargetUnitId"/> 为无单位时生效。</summary>
    public float TargetPosZ {
        get; init;
    }

    /// <summary>移动命令：方向取输入的 X/Y 两分量，零向量即静止。</summary>
    public static PlayerCommand Move(UnitId sourceUnitId, float moveX, float moveY) => new() {
        SourceUnitId = sourceUnitId,
        Kind = PlayerCommandKind.Move,
        MoveDir = new Vector2(moveX, moveY),
    };

    /// <summary>施法命令：目标与锚点按原样承载，取舍在 <see cref="CastTargetPos"/>。</summary>
    public static PlayerCommand Cast(UnitId sourceUnitId, string skillKey, UnitId targetUnitId, float targetX, float targetZ) => new() {
        SourceUnitId = sourceUnitId,
        Kind = PlayerCommandKind.Cast,
        SkillKey = skillKey,
        TargetUnitId = targetUnitId,
        TargetPosX = targetX,
        TargetPosZ = targetZ,
    };

    /// <summary>聚焦命令。</summary>
    public static PlayerCommand Focus(UnitId sourceUnitId, UnitId targetUnitId) => new() {
        SourceUnitId = sourceUnitId,
        Kind = PlayerCommandKind.Focus,
        TargetUnitId = targetUnitId,
    };

    /// <summary>施法位置锚点：单位目标丢弃锚点，位置目标取 XZ 平面坐标。</summary>
    public Vector2? CastTargetPos => TargetUnitId.IsDefault ? new Vector2(TargetPosX, TargetPosZ) : null;
}
