using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Replay;

/// <summary>
/// 回放引擎：解码后的回放在本地用战斗世界确定性重跑。
/// 与在线端共用同一 BattleScene 与输入门面 <see cref="BattleIntentHub"/>，故 ID 解析、排队与落点不会分叉。
/// 每帧顺序与服务端 BattleLoop 钩子一致：门面预备 → 输入注入 → Tick。纯本地零网络依赖，Godot 主线程逐帧驱动。
/// 世界重建照录制端的单位初始态表，实体 ID 与阵营取记录值，属性按配置键取当前配置。
/// </summary>
public sealed class ReplayEngine : IBattleViewSource {
    private readonly BattleScene _battleScene;
    private readonly BattleIntentHub _intentHub;
    private readonly IUnitRegistry _unitRegistry;
    private readonly IReadOnlyList<ReplayUnitInit> _units;
    private readonly ReplayMeta _meta;
    private readonly ReplayMoveRun[][] _moveRunsByPlayer;
    private readonly int[] _moveCursor;
    private readonly List<ReplayCastEntry> _casts;
    private readonly List<ReplayFocusEntry> _focuses;
    private readonly UnitId[] _playerUnitIdByIndex;
    private readonly int _startTick;
    private readonly float _dt;

    private int _castCursor;
    private int _focusCursor;
    private int _frame;

    /// <summary>当前逻辑帧，战斗开始后第 N tick。</summary>
    public int Frame => _frame;

    /// <summary>战斗是否已结束。</summary>
    public bool IsFinished => _battleScene.IsFinished;

    /// <summary>战斗世界全部单位，展示层直读状态。</summary>
    public IReadOnlyList<IUnitUiView> Units => _battleScene.BattleUnits;

    /// <summary>按单位 ID 查战斗单位，不存在返回 null。</summary>
    public IUnitUiView? FindUnit(UnitId unitId) => _battleScene.FindUnit(unitId) as IUnitUiView;

    /// <summary>固定逻辑步长秒数。</summary>
    public float FixedDelta => _dt;

    /// <summary>构建回放：使用默认配置注册表；配置缺失属录制环境不一致，响亮失败。</summary>
    public ReplayEngine(ReplayRecording recording)
        : this(recording, UnitRegistry.Instance, DungeonRegistry.Instance) {
    }

    /// <summary>构建回放：注入配置注册表做双重版本门控、按单位初始态构建战斗世界并立即开战。</summary>
    public ReplayEngine(ReplayRecording recording, IUnitRegistry unitRegistry, IDungeonRegistry dungeonRegistry) {
        _meta = recording.Meta;
        _unitRegistry = unitRegistry;
        _startTick = _meta.StartTick;
        if (_meta.TickRate <= 0)
            throw new InvalidDataException($"Replay invalid tick rate: {_meta.TickRate}.");
        _dt = 1f / _meta.TickRate;
        _units = recording.Units;
        _casts = [.. recording.Casts.OrderBy(c => c.Frame)];
        _focuses = [.. recording.Focuses.OrderBy(f => f.Frame)];
        _playerUnitIdByIndex = [.. _meta.Players.Select(p => p.NetId)];
        (_moveRunsByPlayer, _moveCursor) = BuildMoveTracks(recording.MoveTracks, _meta.Players.Count);

        // 双重门控：内容修订号管配置与布局，逻辑修订号管结算时序，任一不符重算都不可能对上
        if (_meta.DataVersion != GameConfigDB.DataRevision)
            throw new InvalidDataException(
                $"Replay content mismatch: record data={_meta.DataVersion}, current={GameConfigDB.DataRevision}.");
        if (_meta.LogicVersion != BattleLogicRevision.Value)
            throw new InvalidDataException(
                $"Replay logic mismatch: record logic={_meta.LogicVersion}, current={BattleLogicRevision.Value}.");

        var dungeon = dungeonRegistry.GetByKey(_meta.DungeonKey)
            ?? throw new InvalidDataException($"Replay references unknown dungeon key: {_meta.DungeonKey}");
        var movementScene = new PhysicsMovementScene(dungeonRegistry.GetMovementLayout(_meta.DungeonKey));
        _battleScene = new BattleScene(dungeon.RelationsResolver, movementScene);
        _intentHub = new BattleIntentHub(_battleScene);
        BuildUnits();
        _battleScene.CurrentPhase = BattlePhase.Running;
    }

    /// <summary>
    /// 按录制的单位初始态重建全部单位：ID、阵营与出生点取记录值，战斗属性按配置键取当前配置。
    /// 玩家与敌人同表同序，唯一区别是 AI 驱动——玩家单位的 Intelligence 恒为空，操作权在输入轨道。
    /// </summary>
    private void BuildUnits() {
        foreach (var unit in _units) {
            var config = _unitRegistry.GetByKey(unit.UnitConfigKey)
                ?? throw new InvalidDataException($"Replay references unknown unit config: {unit.UnitConfigKey}");
            AddUnit(new BattleUnit {
                UnitId = unit.NetId,
                UnitName = config.ConfigKey,
                Camps = unit.Camps,
                Skills = config.Skills,
                Intelligence = IsPlayerUnit(unit.NetId) ? null : config.Intelligence,
                HateRule = config.HateRule,
                HateFactor = config.HateFactor,
                MaxHealth = config.MaxHealth,
                Health = config.MaxHealth,
                PhysicalAttackBase = config.PhysicalAttackBase,
                PhysicalTakePercent = config.PhysicalTakePercent,
                MagicAttackBase = config.MagicAttackBase,
                MagicTakePercent = config.MagicTakePercent,
                CureIntensity = config.CureIntensity,
                BaseSpeed = config.BaseSpeed,
                BodyRadius = config.BodyRadius,
                Position = new Vector2(unit.SpawnX, unit.SpawnY),
            });
        }
    }

    /// <summary>是否玩家单位：由元数据玩家表的 NetId 认定，不在单位初始态里留第二份。</summary>
    private bool IsPlayerUnit(ushort netId) => Array.IndexOf(_playerUnitIdByIndex, (UnitId)netId) >= 0;

    /// <summary>
    /// 移动轨道按玩家序号归位，段序按帧重排以不信任录制端顺序。玩家表超轨道键容量、序号越界、
    /// 同序号重复轨道都属归档不合规范，响亮失败：缺前一条守卫，按玩家遍历的注入循环永不收敛；
    /// 缺后一条，重复轨道静默吃掉先到的整条轨道。
    /// </summary>
    private static (ReplayMoveRun[][], int[]) BuildMoveTracks(IReadOnlyList<ReplayMoveTrack> tracks, int playerCount) {
        if (playerCount > ReplayMoveTrack.MaxPlayers)
            throw new InvalidDataException(
                $"Replay player table holds {playerCount} players, above move track capacity {ReplayMoveTrack.MaxPlayers}.");

        var runsByPlayer = new ReplayMoveRun[playerCount][];
        for (int i = 0; i < playerCount; i++)
            runsByPlayer[i] = [];

        var claimed = new bool[playerCount];
        foreach (var track in tracks) {
            if (track.PlayerIndex >= playerCount)
                throw new InvalidDataException($"Move track for player index {track.PlayerIndex} exceeds player table.");
            if (claimed[track.PlayerIndex])
                throw new InvalidDataException($"Duplicate move track for player index {track.PlayerIndex}.");
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

        // 与服务端同序：门面预备意图 → 注入本帧记录的新输入 → 推进战斗世界
        _intentHub.PrepareTick(_dt);
        InjectInputs();
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

    /// <summary>重置到战斗开始帧：先经门面丢弃持旧单位引用的在架意图，再重建战斗世界与单位。</summary>
    private void Reset() {
        Array.Clear(_moveCursor);
        _castCursor = 0;
        _focusCursor = 0;
        _frame = 0;
        _intentHub.ClearQueuedCasts();
        foreach (var unit in _battleScene.BattleUnits.ToArray())
            _battleScene.RemoveUnit(unit);
        BuildUnits();
        _battleScene.CurrentPhase = BattlePhase.Running;
    }
}

