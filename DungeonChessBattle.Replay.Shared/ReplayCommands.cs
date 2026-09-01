using DungeonChessBattle.Battle.Shared.Inputs;

namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// 玩家命令与回放记录条目的双向映射唯一权威：载荷拆分口径只有一份。
/// 键的换算不在此——命令持网络 ID，条目持玩家表序号（<see cref="ReplayPlayerInfo"/> 下标），两端各自反查。
/// Accepted 只随施法与聚焦条目落盘：移动无拒绝分支；未被权威接管的条目重放时跳过。
/// </summary>
public static class ReplayCommands {
    /// <summary>移动命令 → 条目。</summary>
    public static MoveInputRecord ToMoveRecord(this PlayerCommand cmd, int frame, byte playerIndex) =>
        new(frame, playerIndex, cmd.MoveDir.X, cmd.MoveDir.Y);

    /// <summary>施法命令 → 条目，技能键按原样落盘，合法性只体现在接管结论里。</summary>
    public static CastSkillRecord ToCastRecord(this PlayerCommand cmd, int frame, byte playerIndex, bool accepted) =>
        new(frame, playerIndex, cmd.SkillKey ?? string.Empty, cmd.TargetNetId, cmd.TargetPosX, cmd.TargetPosZ, accepted);

    /// <summary>聚焦命令 → 条目。</summary>
    public static FocusTargetRecord ToFocusRecord(this PlayerCommand cmd, int frame, byte playerIndex, bool accepted) =>
        new(frame, playerIndex, cmd.TargetNetId, accepted);

    /// <summary>移动条目 → 命令，netId 由玩家序号经头部玩家表反查。</summary>
    public static PlayerCommand ToCommand(this MoveInputRecord record, ushort netId) =>
        PlayerCommand.Move(netId, record.MoveX, record.MoveY);

    /// <summary>施法条目 → 命令。</summary>
    public static PlayerCommand ToCommand(this CastSkillRecord record, ushort netId) =>
        PlayerCommand.Cast(netId, record.SkillTypeId, record.TargetNetId, record.TargetPosX, record.TargetPosZ);

    /// <summary>聚焦条目 → 命令。</summary>
    public static PlayerCommand ToCommand(this FocusTargetRecord record, ushort netId) =>
        PlayerCommand.Focus(netId, record.TargetNetId);
}
