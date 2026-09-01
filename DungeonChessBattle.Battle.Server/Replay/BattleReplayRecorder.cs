using DungeonChessBattle.Battle.Shared.Inputs;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Battle.Server.Replay;

/// <summary>
/// 战斗输入回放记录器：内存存储与逻辑帧时间轴，回放端经快照消费。
/// 只收玩家命令，字段拆分交 <see cref="ReplayCommands"/>，本类只管时间轴与分表。
/// 记录方法仅房间线程调用；快照供任意线程安全读取。
/// </summary>
/// <param name="roomId">房间 ID。</param>
/// <param name="dungeonKey">副本键。</param>
/// <param name="startUnixTime">战斗开始 Unix 秒。</param>
/// <param name="tickRate">逻辑 tick 频率。</param>
/// <param name="players">玩家初始状态表，其下标即记录条目里的玩家序号。</param>
internal sealed class BattleReplayRecorder(string roomId, string dungeonKey, long startUnixTime,
    int tickRate, IReadOnlyList<ReplayPlayerInfo> players) {
    private readonly Lock _lock = new();
    private readonly List<MoveInputRecord> _moveInputs = [];
    private readonly List<CastSkillRecord> _castSkills = [];
    private readonly List<FocusTargetRecord> _focusTargets = [];

    /// <summary>网络 ID → 玩家序号；不在表中即非玩家单位，其命令不入记录。</summary>
    private readonly Dictionary<ushort, byte> _playerIndexByNetId = ToIndexByNetId(players);

    // 头部基础元数据，构造时固定
    private readonly string _roomId = roomId;
    private readonly string _dungeonKey = dungeonKey;
    private readonly long _startUnixTime = startUnixTime;
    private readonly int _tickRate = tickRate;
    private readonly IReadOnlyList<ReplayPlayerInfo> _players = players;

    /// <summary>战斗开始逻辑帧，StartBattle 时写入。</summary>
    private int _startTick;

    /// <summary>服务端最后一个玩家单位 ID + 1，全部单位创建完成后写入，回放端据此分配敌人 ID。</summary>
    private ushort _nextNetId;

    /// <summary>绝对逻辑帧，以首条记录的 tick 锚定，后续按 tick 差分递增，规避 LES ushort tick 回绕。</summary>
    private int _absoluteFrame;

    /// <summary>上次记录时的原始 tick，差分推进绝对帧。</summary>
    private ushort _lastTick;

    private bool _frameInitialized;

    /// <summary>记录战斗开始逻辑帧，StartBattle 时由房间线程写入。</summary>
    public void SetStartTick(int startTick) {
        lock (_lock) {
            _startTick = startTick;
        }
    }

    /// <summary>记录服务端最后一个玩家单位 ID + 1，全部单位创建完成后由房间线程写入。</summary>
    public void SetNextNetId(ushort nextNetId) {
        lock (_lock) {
            _nextNetId = nextNetId;
        }
    }

    /// <summary>
    /// 记录一条玩家命令：按命令类型落到对应条目表，帧由原始 tick 推进而来。
    /// 非玩家单位的命令直接忽略；<paramref name="accepted"/> 是权威投递结论，只有施法与聚焦条目携带。
    /// </summary>
    public void Record(ushort tick, in PlayerCommand cmd, bool accepted) {
        lock (_lock) {
            if (!_playerIndexByNetId.TryGetValue(cmd.NetId, out byte index))
                return;

            int frame = AdvanceFrame(tick);
            switch (cmd.Kind) {
                case PlayerCommandKind.Move:
                    _moveInputs.Add(cmd.ToMoveRecord(frame, index));
                    break;
                case PlayerCommandKind.Cast:
                    _castSkills.Add(cmd.ToCastRecord(frame, index, accepted));
                    break;
                case PlayerCommandKind.Focus:
                    _focusTargets.Add(cmd.ToFocusRecord(frame, index, accepted));
                    break;
            }
        }
    }

    /// <summary>导出只读快照，跨线程安全。</summary>
    public ReplayRecordSnapshot GetSnapshot() {
        lock (_lock) {
            var header = new ReplayRecordHeader(ReplayFormatVersion.Current, _roomId, _dungeonKey,
                _startUnixTime, _tickRate, _players, _startTick, _nextNetId, GameConfigDB.DataRevision);
            return new ReplayRecordSnapshot(header,
                [.. _moveInputs],
                [.. _castSkills],
                [.. _focusTargets]);
        }
    }

    /// <summary>
    /// 把原始 tick 推进到绝对逻辑帧；首条记录锚定自身 tick，同一 tick 多次调用返回同一帧。
    /// ushort 无符号减法回绕安全，前提是相邻记录间隔小于 32768 tick。
    /// </summary>
    private int AdvanceFrame(ushort tick) {
        if (!_frameInitialized) {
            _absoluteFrame = tick;
            _frameInitialized = true;
        }
        else {
            _absoluteFrame += (ushort)(tick - _lastTick);
        }
        _lastTick = tick;
        return _absoluteFrame;
    }

    /// <summary>
    /// 玩家表下标即玩家序号，反查表用于把命令里的网络 ID 换成序号。
    /// 玩家数超出 byte 序号容量属装配错误，构造期响亮失败。
    /// </summary>
    private static Dictionary<ushort, byte> ToIndexByNetId(IReadOnlyList<ReplayPlayerInfo> players) {
        if (players.Count > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(players), players.Count, "Player count exceeds byte index.");
        var indexByNetId = new Dictionary<ushort, byte>(players.Count);
        for (byte i = 0; i < players.Count; i++)
            indexByNetId[players[i].NetId] = i;
        return indexByNetId;
    }
}
