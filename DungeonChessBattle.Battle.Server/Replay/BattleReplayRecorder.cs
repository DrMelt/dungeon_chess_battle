using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Battle.Server.Replay;

/// <summary>
/// 战斗输入回放记录器：内存存储与逻辑帧时间轴，回放端经快照消费。
/// 记录方法仅房间线程调用；快照供任意线程安全读取。
/// 达到 <see cref="MaxEntryCount"/> 后停止记录，并把头部 Complete 置为不完整，避免失控增长。
/// </summary>
/// <param name="roomId">房间 ID。</param>
/// <param name="dungeonKey">副本键。</param>
/// <param name="startUnixTime">战斗开始 Unix 秒。</param>
/// <param name="tickRate">逻辑 tick 频率。</param>
/// <param name="players">玩家初始状态表。</param>
internal sealed class BattleReplayRecorder(string roomId, string dungeonKey, long startUnixTime,
    int tickRate, IReadOnlyList<ReplayPlayerInfo> players) {
    /// <summary>记录条目上限。移动输入每 tick 每人一条：128 tick/s 下八人满员约 16 分钟触顶，单人约 2.2 小时。</summary>
    public const int MaxEntryCount = 1_000_000;

    private readonly Lock _lock = new();
    private readonly List<MoveInputRecord> _moveInputs = [];
    private readonly List<CastSkillRecord> _castSkills = [];
    private readonly List<FocusTargetRecord> _focusTargets = [];

    // 头部基础元数据，构造时固定
    private readonly string _roomId = roomId;
    private readonly string _dungeonKey = dungeonKey;
    private readonly long _startUnixTime = startUnixTime;
    private readonly int _tickRate = tickRate;
    private readonly IReadOnlyList<ReplayPlayerInfo> _players = players;

    /// <summary>战斗开始逻辑帧，StartBattle 时写入。</summary>
    private int _startTick;

    /// <summary>服务端最后一个单位 ID + 1，全部单位创建完成后写入，回放端据此分配敌人 ID。</summary>
    private ushort _nextNetId;

    /// <summary>录制是否完整，条目达上限后置 false。</summary>
    private bool _complete = true;

    /// <summary>绝对逻辑帧，以首条记录的 tick 锚定，后续按 tick 差分递增，规避 LES ushort tick 回绕。</summary>
    private int _absoluteFrame;

    /// <summary>上次记录时的原始 tick，差分推进绝对帧。</summary>
    private ushort _lastTick;

    private bool _frameInitialized;
    private bool _full;
    private int _entryCount;

    /// <summary>记录战斗开始逻辑帧，StartBattle 时由房间线程写入。</summary>
    public void SetStartTick(int startTick) {
        lock (_lock) {
            _startTick = startTick;
        }
    }

    /// <summary>记录服务端最后一个单位 ID + 1，全部单位创建完成后由房间线程写入。</summary>
    public void SetNextNetId(ushort nextNetId) {
        lock (_lock) {
            _nextNetId = nextNetId;
        }
    }

    /// <summary>记录移动输入，返回是否已记录；达到上限后返回 false 且不记录。</summary>
    public bool RecordMoveInput(ushort tick, byte playerIndex, float moveX, float moveY) {
        lock (_lock) {
            return TryAppend(() =>
                _moveInputs.Add(new MoveInputRecord(AdvanceFrame(tick), playerIndex, moveX, moveY)));
        }
    }

    /// <summary>记录施法请求与接受结果，返回是否已记录；达到上限后返回 false 且不记录。</summary>
    public bool RecordCastSkill(ushort tick, byte playerIndex, string skillTypeId, ushort targetNetId,
        float targetPosX, float targetPosZ, bool accepted) {
        lock (_lock) {
            return TryAppend(() =>
                _castSkills.Add(new CastSkillRecord(AdvanceFrame(tick), playerIndex,
                    skillTypeId, targetNetId, targetPosX, targetPosZ, accepted)));
        }
    }

    /// <summary>记录聚焦目标请求与接受结果，返回是否已记录；达到上限后返回 false 且不记录。</summary>
    public bool RecordFocusTarget(ushort tick, byte playerIndex, ushort targetNetId, bool accepted) {
        lock (_lock) {
            return TryAppend(() =>
                _focusTargets.Add(new FocusTargetRecord(AdvanceFrame(tick), playerIndex, targetNetId, accepted)));
        }
    }

    /// <summary>导出只读快照，跨线程安全。</summary>
    public ReplayRecordSnapshot GetSnapshot() {
        lock (_lock) {
            var header = new ReplayRecordHeader(ReplayFormatVersion.Current, _roomId, _dungeonKey,
                _startUnixTime, _tickRate, _players, _startTick, _nextNetId, _complete, GameConfigDB.DataRevision);
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
    /// 追加一条记录；达到条目上限后置满并拒绝，返回是否已记录。
    /// 上限判定在追加前，保证上限内条目全部有效，返回 false 恒表示本条未记录。
    /// </summary>
    private bool TryAppend(Action append) {
        if (_full || _entryCount >= MaxEntryCount) {
            _full = true;
            _complete = false;
            return false;
        }
        append();
        _entryCount++;
        return true;
    }
}
