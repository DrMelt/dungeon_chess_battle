using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Inputs;

namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// 玩家命令与回放条目的双向映射唯一权威：载荷拆分口径只有一份。
/// 键的换算不在此——命令持 <see cref="UnitId"/>，条目持玩家表序号（<see cref="ReplayMeta.Players"/> 下标）
/// 与 ushort 目标 ID，两端各自反查；命令与条目之间的 ID 升降级只在本类这组映射里发生。
/// Accepted 只随施法与聚焦条目落盘：移动无拒绝分支；未被权威接管的条目重放时跳过。
/// 移动段的收拢也在此：账本骨架、续段判据与轨道成型一处收口，录制端只持时间轴与账本。
/// 逐 tick 提交语义不变，收拢只发生在存储侧。
/// </summary>
public static class ReplayCommands {
    /// <summary>移动命令 → 新方向意图段，长度 1，续段交 <see cref="ExtendRun"/>。</summary>
    public static ReplayMoveRun NewMoveRun(this in PlayerCommand cmd, int frame) =>
        new(frame, 1, cmd.MoveDir.X, cmd.MoveDir.Y);

    /// <summary>移动命令的方向与既有段是否位相同；NaN 与 ±0 各自成段，比较口径唯一。</summary>
    public static bool SameMoveDir(this in PlayerCommand cmd, in ReplayMoveRun run) =>
        Bits(cmd.MoveDir.X) == Bits(run.DirX) && Bits(cmd.MoveDir.Y) == Bits(run.DirY);

    /// <summary>方向意图段延长一帧，段首帧与方向不动。</summary>
    public static ReplayMoveRun ExtendRun(in ReplayMoveRun run) => run with { Length = run.Length + 1 };

    /// <summary>
    /// 逐玩家移动账本的骨架：每个玩家序号一条待填轨道。玩家数容量在此守住，
    /// <see cref="BuildMoveTracks"/> 才敢把数组下标降型成 byte。
    /// </summary>
    public static List<ReplayMoveRun>[] CreateMoveTracks(int playerCount) {
        if (playerCount > ReplayMoveTrack.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Player count exceeds move track capacity.");

        var tracks = new List<ReplayMoveRun>[playerCount];
        for (int i = 0; i < playerCount; i++)
            tracks[i] = [];
        return tracks;
    }

    /// <summary>
    /// 把一条移动命令折进该玩家轨道：帧连续且方向位相同即续段，否则另起一段。
    /// 输入断供处帧不连续，自然断段。移动每 tick 至多一条是录制端前提（见 BattleReplayRecorder），
    /// 同 tick 第二条移动命令不在本判据覆盖内。
    /// </summary>
    public static void AppendMoveRun(List<ReplayMoveRun> runs, in PlayerCommand cmd, int frame) {
        if (runs.Count > 0) {
            ReplayMoveRun last = runs[^1];
            if (last.EndFrame + 1 == frame && cmd.SameMoveDir(in last)) {
                runs[^1] = ExtendRun(in last);
                return;
            }
        }

        runs.Add(cmd.NewMoveRun(frame));
    }

    /// <summary>逐玩家账本折叠为轨道表：空轨不产出，数组下标即玩家序号。</summary>
    public static ReplayMoveTrack[] BuildMoveTracks(List<ReplayMoveRun>[] runs) {
        if (runs.Length > ReplayMoveTrack.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(runs), runs.Length, "Track count exceeds move track capacity.");

        var tracks = new List<ReplayMoveTrack>(runs.Length);
        for (int i = 0; i < runs.Length; i++) {
            if (runs[i].Count > 0)
                tracks.Add(new ReplayMoveTrack((byte)i, [.. runs[i]]));
        }

        return [.. tracks];
    }

    /// <summary>施法命令 → 条目，技能键按原样落盘，合法性只体现在接管结论里。</summary>
    public static ReplayCastEntry ToCastEntry(this in PlayerCommand cmd, int frame, byte playerIndex, bool accepted) =>
        new(frame, playerIndex, cmd.SkillKey ?? string.Empty, cmd.TargetUnitId, cmd.TargetPosX, cmd.TargetPosZ, accepted);

    /// <summary>聚焦命令 → 条目。</summary>
    public static ReplayFocusEntry ToFocusEntry(this in PlayerCommand cmd, int frame, byte playerIndex, bool accepted) =>
        new(frame, playerIndex, cmd.TargetUnitId, accepted);

    /// <summary>方向意图段 → 移动命令，来源单位由玩家序号经元数据玩家表反查；段内每帧重投同一条。</summary>
    public static PlayerCommand ToCommand(this in ReplayMoveRun run, UnitId sourceUnitId) =>
        PlayerCommand.Move(sourceUnitId, run.DirX, run.DirY);

    /// <summary>施法条目 → 命令。</summary>
    public static PlayerCommand ToCommand(this in ReplayCastEntry entry, UnitId sourceUnitId) =>
        PlayerCommand.Cast(sourceUnitId, entry.SkillKey, entry.TargetNetId, entry.TargetPosX, entry.TargetPosZ);

    /// <summary>聚焦条目 → 命令。</summary>
    public static PlayerCommand ToCommand(this in ReplayFocusEntry entry, UnitId sourceUnitId) =>
        PlayerCommand.Focus(sourceUnitId, entry.TargetNetId);

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
}
