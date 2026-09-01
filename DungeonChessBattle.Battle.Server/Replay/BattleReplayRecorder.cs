using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Inputs;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Battle.Server.Replay;

/// <summary>
/// 战斗输入回放录制器：绝对逻辑帧时间轴 + 移动按玩家分轨的账本与施法、聚焦两本条目账，房间销毁时导出 <see cref="ReplayRecording"/>。
/// 只收玩家命令；字段拆分、段折叠判据与轨道成型全在 <see cref="ReplayCommands"/>，两项修订号由调用方供给——
/// 本类既不解释条目形状，也不认识内容版本，只管时间轴推进与账本归属。
/// 记录方法仅房间线程调用，导出供外部线程安全读取。
/// 移动每 tick 至多一条，由 <c>UnitController.BeforeControlledUpdate</c> 每 tick 消费一次输入后经 Pawn 输入事件
/// 转发到房间 <c>OnPawnInput</c> 提交；单位失去控制或输入断供造成帧空洞，空洞处即段界。
/// 重放端展开回逐 tick 提交，结果与逐条记录一致。
/// </summary>
/// <param name="roomId">房间 ID。</param>
/// <param name="dungeonKey">副本键。</param>
/// <param name="startUnixTime">战斗开始 Unix 秒。</param>
/// <param name="tickRate">逻辑 tick 频率。</param>
/// <param name="players">参与玩家表，其下标即条目里的玩家序号。</param>
internal sealed class BattleReplayRecorder(string roomId, string dungeonKey, long startUnixTime,
    int tickRate, IReadOnlyList<ReplayPlayerInfo> players) {
    private readonly Lock _lock = new();
    private readonly List<ReplayCastEntry> _casts = [];
    private readonly List<ReplayFocusEntry> _focuses = [];
    private readonly List<ReplayMoveRun>[] _moveRuns = ReplayCommands.CreateMoveTracks(players.Count);

    /// <summary>单位 ID → 玩家序号；不在表中即非玩家单位，其命令不入记录。</summary>
    private readonly Dictionary<UnitId, byte> _playerIndexByUnitId = ToIndexByUnitId(players);

    /// <summary>单位初始态，全部单位创建完成后写入，重放端据此重建世界。</summary>
    private IReadOnlyList<ReplayUnitInit> _units = [];

    /// <summary>战斗开始逻辑帧，StartBattle 时写入。</summary>
    private int _startTick;

    /// <summary>战斗结束逻辑帧，进入结束态时首写生效；-1 表示战斗未打完。</summary>
    private int _endTick = -1;

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

    /// <summary>记录全部单位初始态，单位创建完成后由房间线程写入一次。</summary>
    public void SetUnits(IReadOnlyList<ReplayUnitInit> units) {
        lock (_lock) {
            _units = units;
        }
    }

    /// <summary>
    /// 记录一条玩家命令：按命令类型落到对应轨道，帧由原始 tick 推进而来。
    /// 非玩家单位的命令直接忽略；<paramref name="accepted"/> 是权威投递结论，只有施法与聚焦条目携带。
    /// </summary>
    public void Record(ushort tick, in PlayerCommand cmd, bool accepted) {
        lock (_lock) {
            if (!_playerIndexByUnitId.TryGetValue(cmd.SourceUnitId, out byte index))
                return;

            int frame = AdvanceFrame(tick);
            switch (cmd.Kind) {
                case PlayerCommandKind.Move:
                    ReplayCommands.AppendMoveRun(_moveRuns[index], cmd, frame);
                    break;
                case PlayerCommandKind.Cast:
                    _casts.Add(cmd.ToCastEntry(frame, index, accepted));
                    break;
                case PlayerCommandKind.Focus:
                    _focuses.Add(cmd.ToFocusEntry(frame, index, accepted));
                    break;
            }
        }
    }

    /// <summary>记录战斗结束逻辑帧，首写生效：结束之后的帧不进回放。</summary>
    public void MarkEnd(ushort tick) {
        lock (_lock) {
            if (_endTick >= 0)
                return;
            _endTick = AdvanceFrame(tick);
        }
    }

    /// <summary>
    /// 导出可编码的回放内容；战斗未打完时结束帧退到最后一条记录帧。
    /// 两项修订号由调用方读好交进来，本类只把它们写进元数据，不决定其值。
    /// </summary>
    public ReplayRecording BuildRecording(string dataRevision, string logicRevision) {
        lock (_lock) {
            int lastFrame = _frameInitialized ? _absoluteFrame : _startTick;
            var meta = new ReplayMeta(roomId, dungeonKey, startUnixTime, tickRate, _startTick,
                Math.Max(_startTick, _endTick >= 0 ? _endTick : lastFrame),
                dataRevision, logicRevision, players);

            return new ReplayRecording(meta, _units, ReplayCommands.BuildMoveTracks(_moveRuns),
                [.. _casts], [.. _focuses]);
        }
    }

    /// <summary>
    /// 把原始 tick 推进到绝对逻辑帧；首条记录锚定自身 tick，同一 tick 的多次调用返回同一帧——
    /// 施法与聚焦可在同一 tick 各来一条，移动每 tick 至多一条，故同帧多条命令必分属不同轨道。
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
    /// 玩家表下标即玩家序号，反查表用于把命令里的来源单位换成序号。
    /// 玩家数超出轨道键容量属装配错误，构造期响亮失败；序号由 int 下标降型，容量口径见 <see cref="ReplayMoveTrack.MaxPlayers"/>。
    /// </summary>
    private static Dictionary<UnitId, byte> ToIndexByUnitId(IReadOnlyList<ReplayPlayerInfo> players) {
        if (players.Count > ReplayMoveTrack.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(players), players.Count, "Player count exceeds move track capacity.");
        var indexByUnitId = new Dictionary<UnitId, byte>(players.Count);
        for (int i = 0; i < players.Count; i++)
            indexByUnitId[players[i].NetId] = (byte)i;
        return indexByUnitId;
    }
}
