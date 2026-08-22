using DungeonChessBattle.Entities.Replay;

namespace DungeonChessBattle.Server.Battle.Replay;

/// <summary>
/// 战斗输入回放记录器：内存存储与逻辑帧时间轴，回放端经快照消费。
/// 记录方法仅房间线程调用；快照供任意线程安全读取。
/// 达到 <see cref="MaxEntryCount"/> 后停止记录，避免失控增长。
/// </summary>
/// <param name="header">回放记录头部元数据，由房间初始化时构建。</param>
internal sealed class BattleReplayRecorder(ReplayRecordHeader header) {
    /// <summary>记录条目上限，50tick/s 满员 30 分钟约 72 万条移动输入。</summary>
    public const int MaxEntryCount = 1_000_000;

    private readonly Lock _lock = new();
    private readonly List<MoveInputRecord> _moveInputs = [];
    private readonly List<CastSkillRecord> _castSkills = [];
    private readonly List<FocusTargetRecord> _focusTargets = [];

    /// <summary>绝对逻辑帧，以首条记录的 tick 锚定，后续按 tick 差分递增，规避 LES ushort tick 回绕。</summary>
    private int _absoluteFrame;
    /// <summary>上次记录时的原始 tick，差分推进绝对帧。</summary>
    private ushort _lastTick;
    private bool _frameInitialized;
    private bool _full;
    private int _entryCount;

    /// <summary>回放记录头部元数据。</summary>
    public ReplayRecordHeader Header {
        get;
    } = header;

    /// <summary>记录移动输入，返回是否已记录；达到上限后返回 false 且不记录。</summary>
    public bool RecordMoveInput(ushort tick, byte playerIndex, float moveX, float moveY) {
        lock (_lock) {
            return TryAppend(() =>
                _moveInputs.Add(new MoveInputRecord(AdvanceFrame(tick), playerIndex, moveX, moveY)));
        }
    }

    /// <summary>记录施法请求与接受结果，返回是否已记录；达到上限后返回 false 且不记录。</summary>
    public bool RecordCastSkill(ushort tick, byte playerIndex, ushort skillTypeId, ushort targetNetId,
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
            return new ReplayRecordSnapshot(Header,
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
            return false;
        }
        append();
        _entryCount++;
        return true;
    }
}
