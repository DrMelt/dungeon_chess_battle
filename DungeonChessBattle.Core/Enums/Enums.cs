namespace DungeonChessBattle.Core.Enums;

/// <summary>
/// 伤害类型。
/// </summary>
public enum DamageType {
    /// <summary>无伤害。</summary>
    None = 0,
    /// <summary>物理伤害。</summary>
    Physical,
    /// <summary>魔法伤害。</summary>
    Magic,
}

/// <summary>
/// 技能可释放目标类型的 Flag 位标志。
/// 通过 HasFlag 判断目标属于同阵营或敌阵营。
/// </summary>
[Flags]
public enum SkillCanAdd {
    /// <summary>无类型限制。</summary>
    None = 0,
    /// <summary>可对同阵营单位释放。</summary>
    Same = 1,
    /// <summary>可对敌阵营单位释放。</summary>
    Different = 2,
}

/// <summary>
/// 玩家连接状态（用于断线重连）。
/// </summary>
public enum PlayerConnectionState : byte {
    /// <summary>已连接。</summary>
    Connected = 0,
    /// <summary>已断开。</summary>
    Disconnected = 1,
}

/// <summary>
/// 战斗阶段（实时化：去掉回合制概念）。
/// </summary>
public enum BattlePhase : byte {
    /// <summary>等待开始（大厅→战斗过渡）。</summary>
    Waiting,
    /// <summary>战斗中（实时 Tick）。</summary>
    Running,
    /// <summary>战斗结束。</summary>
    Finished,
}

/// <summary>
/// 房间分类（招募板使用）。
/// </summary>
public enum RoomCategory : byte {
    /// <summary>休闲房间。</summary>
    Casual = 0,
    /// <summary>竞技房间。</summary>
    Competitive = 1,
    /// <summary>练习房间。</summary>
    Practice = 2,
    /// <summary>锦标赛房间。</summary>
    Tournament = 3,
}

/// <summary>
/// 房间状态（招募板使用）。
/// </summary>
public enum RoomStatus : byte {
    /// <summary>等待中。</summary>
    Waiting = 0,
    /// <summary>进行中。</summary>
    InProgress = 1,
    /// <summary>已结束。</summary>
    Finished = 2,
}
