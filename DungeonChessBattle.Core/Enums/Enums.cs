namespace DungeonChessBattle.Core.Enums;

public enum EnumCamp {
    None = 0,
    Camp_A,
    Camp_B,
    Camp_BOSS,
    EnumCampLength,
}

public enum Enum_DamageType {
    None = 0,
    Physcial,
    Magic,
}

[Flags]
public enum EnumSkillCanAdd {
    None = 0,
    Same = 1,
    Different = 2,
}

/// <summary>
/// 玩家连接状态（用于断线重连）。
/// </summary>
public enum PlayerConnectionState : byte {
    Connected = 0,
    Disconnected = 1,
}

/// <summary>
/// 战斗阶段（实时化：去掉回合制概念）。
/// </summary>
public enum BattlePhase : byte {
    Waiting,   // 等待开始（大厅→战斗过渡）
    Running,   // 战斗中（实时 Tick）
    Finished,  // 战斗结束
}
