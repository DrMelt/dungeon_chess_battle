using System.Linq;
using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Runtime.Shared.Combat;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Control;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Battle.Config.Shared;
using DungeonChessBattle.Battle.Config.Shared.Content;
using ErrorOr;

namespace DungeonChessBattle.Replay;

/// <summary>
/// 回放引擎：解码后的回放在本地用战斗世界确定性重跑。
/// 与在线端共用同一 BattleScene 与输入门面 <see cref="BattleIntentHub"/>，故单位 ID 解析与落点不会分叉。
/// 每帧顺序与服务端 BattleLoop 钩子一致：输入注入 → 门面预备 → Tick。纯本地零网络依赖，Godot 主线程逐帧驱动。
/// 世界重建照录制端的单位初始态表，实体 ID 与阵营取记录值，属性按配置键取当前配置。
/// 本类只承担重放驱动与世界读数，不实现表现层数据源接口——展示取数由 Game 层统一数据源
/// <c>BattleSessionContext</c> 经此处只读成员装配。
/// </summary>
public sealed class ReplayEngine {
    private readonly BattleScene _battleScene;
    private readonly UnitIntentDriver _intentDriver;
    private readonly BattleIntentHub _intentHub;
    private readonly IContentRegistryView _content;
    private readonly IReadOnlyList<ReplayUnitInit> _units;
    private readonly ReplayMeta _meta;
    private readonly ReplayMoveRun[][] _moveRunsByPlayer;
    private readonly int[] _moveCursor;
    private readonly List<ReplayCastEntry> _casts;
    private readonly List<ReplayFocusEntry> _focuses;
    private readonly UnitId[] _playerUnitIdByIndex;
    private readonly ReplayInputTimeline _inputs;
    private readonly int _startTick;
    private readonly float _dt;

    private int _castCursor;
    private int _focusCursor;
    private int _frame;

    /// <summary>当前逻辑帧，战斗开始后第 N tick。</summary>
    public int Frame => _frame;

    /// <summary>战斗是否已结束。</summary>
    public bool IsFinished => _battleScene.IsFinished;

    /// <summary>战斗世界全部单位，表现层数据源装配的唯一取数口。</summary>
    public IReadOnlyList<IUnitUiView> Units => _battleScene.BattleUnits;

    /// <summary>按单位 ID 查战斗单位，不存在返回 null。</summary>
    public IUnitUiView? FindUnit(UnitId unitId) => _battleScene.FindUnit(unitId) as IUnitUiView;

    /// <summary>归档记录的副本键，展示层据此装配副本环境与阵营关系。</summary>
    public string DungeonKey => _meta.DungeonKey;

    /// <summary>录制端的战斗开始时刻，UTC Unix 秒。</summary>
    public long BattleStartUnixTime => _meta.StartUnixTime;

    /// <summary>已重放的逻辑秒数，由帧轴与固定步长推算。</summary>
    public double ElapsedSeconds => _frame * (double)_dt;

    /// <summary>当前帧的 UTC Unix 毫秒：录制开始时刻加帧轴，事件日志与在线接收时刻同数轴。</summary>
    public long FrameUnixMs => _meta.StartUnixTime * 1000L + (long)(ElapsedSeconds * 1000.0);

    /// <summary>玩家表，下标即输入条目的玩家序号。</summary>
    public IReadOnlyList<ReplayPlayerInfo> Players => _meta.Players;

    /// <summary>输入条目时间轴：构造期一次性建好的只读投影，不随推进变化。</summary>
    public ReplayInputTimeline Inputs => _inputs;

    /// <summary>战斗开始的绝对逻辑帧，条目帧号与帧轴互转的唯一偏移量。</summary>
    public int StartTick => _startTick;

    /// <summary>固定逻辑步长秒数。</summary>
    public float FixedDelta => _dt;

    /// <summary>
    /// 构建回放：注入内容注册表只读视图，归档门控、副本引用、移动轨道与单位引用先全部裁决，
    /// 通过后按单位初始态装配战斗世界并立即开战。被拒的归档不做三表混排与单位构建。
    /// </summary>
    public static ErrorOr<ReplayEngine> Create(ReplayRecording recording, IContentRegistryView content) {
        ReplayMeta meta = recording.Meta;
        if (meta.TickRate <= 0)
            return ReplayErrors.InvalidTickRate(meta.TickRate);

        // 双重门控：内容修订号管配置与布局，逻辑修订号管结算时序，任一不符重算都不可能对上
        if (meta.DataVersion != content.DataRevision)
            return ReplayErrors.ContentMismatch(meta.DataVersion, content.DataRevision);
        if (meta.LogicVersion != BattleLogicRevision.Value)
            return ReplayErrors.LogicMismatch(meta.LogicVersion, BattleLogicRevision.Value);

        // 归档携带的副本键先过值对象校验：空或超长即拒绝，不把非法键带进内容查询
        if (RestrictedString.TryCreate(meta.DungeonKey, DungeonKeyId.MaxLength) is not { } key)
            return ReplayErrors.InvalidDungeonKey(meta.DungeonKey);
        if (content.GetDungeon(key.Value) is not { } dungeon)
            return ReplayErrors.UnknownDungeonKey(meta.DungeonKey);

        ErrorOr<(ReplayMoveRun[][] Runs, int[] Cursors)> tracks =
            BuildMoveTracks(recording.MoveTracks, meta.Players.Count);
        if (tracks.IsError)
            return tracks.FirstError;

        // 单位初始态引用的配置必须在场：重建路径按同一份表取值，不再逐个体复核
        ReplayUnitInit? missing = recording.Units
            .FirstOrDefault(unit => content.GetUnit(unit.UnitConfigKey) is null);
        if (missing is not null)
            return ReplayErrors.UnknownUnitConfig(missing.UnitConfigKey);

        return new ReplayEngine(recording, content, dungeon, tracks.Value.Runs, tracks.Value.Cursors);
    }

    /// <summary>装配回放：门控与内容引用已由 <see cref="Create"/> 裁决，本构造只建只读投影与战斗世界。</summary>
    private ReplayEngine(ReplayRecording recording, IContentRegistryView content, DungeonConfig dungeon,
        ReplayMoveRun[][] moveRunsByPlayer, int[] moveCursor) {
        _meta = recording.Meta;
        _content = content;
        _startTick = _meta.StartTick;
        _dt = 1f / _meta.TickRate;
        _units = recording.Units;
        _casts = [.. recording.Casts.OrderBy(c => c.Frame)];
        _focuses = [.. recording.Focuses.OrderBy(f => f.Frame)];
        _playerUnitIdByIndex = [.. _meta.Players.Select(p => p.NetId)];
        _moveRunsByPlayer = moveRunsByPlayer;
        _moveCursor = moveCursor;

        // 只读投影建在门后：被拒的归档不必先做一遍三表混排
        _inputs = ReplayInputTimeline.Build(recording);
        _battleScene = new BattleScene(dungeon.RelationsResolver, new PhysicsMovementScene(dungeon.Layout));
        _intentDriver = new UnitIntentDriver(_battleScene, dungeon.RelationsResolver);
        _intentHub = new BattleIntentHub(_battleScene, _intentDriver);
        BuildUnits();
        _battleScene.CurrentPhase = BattlePhase.Running;
    }

    /// <summary>
    /// 按录制的单位初始态重建全部单位：ID、阵营与出生点取记录值，战斗属性按配置键取当前配置。
    /// 玩家与敌人同表同序，意图源按录制玩家表分流登记，与服务器生成路径同一判据。
    /// 单位配置在场由 <see cref="Create"/> 一次裁决，重建路径不再查空。
    /// </summary>
    private void BuildUnits() {
        foreach (var unit in _units) {
            var config = _content.GetUnit(unit.UnitConfigKey)!;
            var battleUnit = new BattleUnit {
                UnitId = unit.NetId,
                UnitName = config.ConfigKey,
                Camps = [.. unit.Camps.Select(camp => new CampId(camp))],
                BaseConfig = config.BaseConfig,
                Skills = config.Skills,
                HateRule = config.HateRule,
                HateFactor = config.HateFactor,
                Health = config.BaseConfig.MaxHealth,
                Position = new Vector2(unit.SpawnX, unit.SpawnY),
            };
            AddUnit(battleUnit);
            if (_playerUnitIdByIndex.Contains(unit.NetId))
                _intentDriver.RegisterPlayer(battleUnit.UnitId);
            else if (config.Controller is { } controller)
                _intentDriver.RegisterAutonomous(battleUnit.UnitId, controller);
        }
    }

    /// <summary>
    /// 移动轨道按玩家序号归位，段序按帧重排以不信任录制端顺序。玩家表超轨道键容量、序号越界、
    /// 同序号重复轨道都属归档不合规范，以错误拒载：缺前一条守卫，按玩家遍历的注入循环永不收敛；
    /// 缺后一条，重复轨道静默吃掉先到的整条轨道。
    /// </summary>
    private static ErrorOr<(ReplayMoveRun[][] Runs, int[] Cursors)> BuildMoveTracks(
        IReadOnlyList<ReplayMoveTrack> tracks, int playerCount) {
        if (playerCount > ReplayMoveTrack.MaxPlayers)
            return ReplayErrors.PlayerTableOverCapacity(playerCount, ReplayMoveTrack.MaxPlayers);

        var runsByPlayer = new ReplayMoveRun[playerCount][];
        for (int i = 0; i < playerCount; i++)
            runsByPlayer[i] = [];

        var claimed = new bool[playerCount];
        foreach (var track in tracks) {
            if (track.PlayerIndex >= playerCount)
                return ReplayErrors.MoveTrackIndexOutOfRange(track.PlayerIndex);
            if (claimed[track.PlayerIndex])
                return ReplayErrors.DuplicateMoveTrack(track.PlayerIndex);
            claimed[track.PlayerIndex] = true;
            runsByPlayer[track.PlayerIndex] = [.. track.Runs.OrderBy(r => r.Frame)];
        }

        return (runsByPlayer, new int[playerCount]);
    }

    /// <summary>注册领域单位到战斗世界。</summary>
    private void AddUnit(BattleUnit unit) => _battleScene.AddUnit(unit);

    /// <summary>
    /// 重建战斗世界并推进到指定逻辑帧；目标帧早于当前帧时先重置再快进。
    /// 拖动与回看共用入口。
    /// </summary>
    public void SeekTo(int targetFrame) {
        if (targetFrame < _frame)
            Reset();
        int guard = 0;
        while (_frame < targetFrame && !_battleScene.IsFinished && guard++ < 1_000_000)
            Step();
    }

    /// <summary>推进一逻辑帧，返回本帧领域事件。战斗结束后返回空。</summary>
    public IReadOnlyList<IBattleEvent> Step() {
        if (_battleScene.IsFinished)
            return [];

        // 与服务端同序：注入本帧记录的新输入 → 门面预备意图 → 推进战斗世界
        InjectInputs();
        _intentHub.PrepareTick(_dt);
        var events = _battleScene.Tick(_dt);
        _frame++;
        return events;
    }

    /// <summary>回放覆盖的总逻辑帧数，取自录制端记下的战斗结束帧。</summary>
    public int TotalFrames => _meta.DurationTicks;

    /// <summary>
    /// 按帧注入玩家命令：施法 → 移动 → 聚焦，三类共享同一帧轴，经与在线同一个输入门面提交。
    /// 施法与移动都只登记意图，同序要求见 <see cref="BattleIntentHub.PrepareTick"/>；<c>Accepted=false</c> 的条目跳过。
    /// 移动按方向意图段展开：段覆盖本帧即重投该段方向，逐 tick 提交语义与在线一致。
    /// </summary>
    private void InjectInputs() {
        int absoluteFrame = _startTick + _frame;

        while (_castCursor < _casts.Count) {
            var c = _casts[_castCursor];
            if (c.Frame > absoluteFrame)
                break;
            if (c.Frame == absoluteFrame && c.Accepted)
                _intentHub.Submit(c.ToCommand(UnitIdOf(c.PlayerIndex)));
            _castCursor++;
        }

        // 循环变量必须是 int：轨道数可达容量上限 256，byte 自增会在末位回绕，令本循环永不收敛
        for (int player = 0; player < _moveRunsByPlayer.Length; player++) {
            var runs = _moveRunsByPlayer[player];
            int cursor = _moveCursor[player];
            while (cursor < runs.Length && runs[cursor].EndFrame < absoluteFrame)
                cursor++;
            _moveCursor[player] = cursor;
            if (cursor < runs.Length && runs[cursor].Frame <= absoluteFrame)
                _intentHub.Submit(ReplayCommands.ToCommand(in runs[cursor], UnitIdOf(player)));
        }

        while (_focusCursor < _focuses.Count) {
            var f = _focuses[_focusCursor];
            if (f.Frame > absoluteFrame)
                break;
            if (f.Frame == absoluteFrame && f.Accepted)
                _intentHub.Submit(f.ToCommand(UnitIdOf(f.PlayerIndex)));
            _focusCursor++;
        }
    }

    /// <summary>玩家序号 → 元数据玩家表里的单位 ID；越界返回 <see cref="UnitId.None"/>，门内解析不到即自然落空。</summary>
    private UnitId UnitIdOf(int playerIndex) =>
        playerIndex < _playerUnitIdByIndex.Length ? _playerUnitIdByIndex[playerIndex] : UnitId.None;

    /// <summary>重置到战斗开始帧：拆除单位后重建，意图源按单位重新登记，在架待决意图随之作废。</summary>
    private void Reset() {
        Array.Clear(_moveCursor);
        _castCursor = 0;
        _focusCursor = 0;
        _frame = 0;
        foreach (var unit in _battleScene.BattleUnits.ToArray())
            _battleScene.RemoveUnit(unit);
        BuildUnits();
        _battleScene.CurrentPhase = BattlePhase.Running;
    }
}

