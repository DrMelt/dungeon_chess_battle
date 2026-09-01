using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Models;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Replay;

/// <summary>
/// 回放引擎：解码后的回放快照在本地用战斗世界确定性重跑。
/// 与在线端共用同一 BattleScene 与输入门面 <see cref="BattleIntentHub"/>，故 ID 解析、排队与落点不会分叉。
/// 每帧顺序与服务端 BattleLoop 钩子一致：门面预备 → 输入注入 → Tick。纯本地零网络依赖，Godot 主线程逐帧驱动。
/// </summary>
public sealed class ReplayEngine : IBattleViewSource {
    private readonly BattleScene _battleScene;
    private readonly BattleIntentHub _intentHub;
    private readonly DungeonConfig _dungeon;
    private readonly IUnitRegistry _unitRegistry;
    private readonly IDungeonRegistry _dungeonRegistry;
    private readonly List<MoveInputRecord> _moves;
    private readonly List<CastSkillRecord> _casts;
    private readonly List<FocusTargetRecord> _focuses;
    private readonly UnitId[] _playerUnitIdByIndex;
    private readonly int _startTick;
    private readonly float _dt;

    private int _moveCursor;
    private int _castCursor;
    private int _focusCursor;
    private int _frame;

    /// <summary>解码后的回放记录。</summary>
    public ReplayRecordSnapshot Snapshot {
        get;
    }

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
    public ReplayEngine(ReplayRecordSnapshot snapshot)
        : this(snapshot, UnitRegistry.Instance, DungeonRegistry.Instance) {
    }

    /// <summary>构建回放：注入配置注册表解析头部、构建战斗世界与单位并立即开始战斗。</summary>
    public ReplayEngine(ReplayRecordSnapshot snapshot, IUnitRegistry unitRegistry, IDungeonRegistry dungeonRegistry) {
        Snapshot = snapshot;
        _unitRegistry = unitRegistry;
        _dungeonRegistry = dungeonRegistry;
        _startTick = snapshot.Header.StartTick;
        _dt = 1f / snapshot.Header.TickRate;
        _moves = [.. snapshot.MoveInputs.OrderBy(m => m.Frame)];
        _casts = [.. snapshot.CastSkills.OrderBy(c => c.Frame)];
        _focuses = [.. snapshot.FocusTargets.OrderBy(f => f.Frame)];
        _playerUnitIdByIndex = [.. snapshot.Header.Players.Select(p => p.NetId)];

        // 内容一致校验：录制端内容修订号与当前不一致即拒绝重放
        if (snapshot.Header.DataVersion != GameConfigDB.DataRevision)
            throw new InvalidDataException(
                $"Replay content mismatch: record data={snapshot.Header.DataVersion}, current={GameConfigDB.DataRevision}.");

        var dungeon = _dungeonRegistry.GetByKey(snapshot.Header.DungeonKey)
            ?? throw new InvalidDataException($"Replay references unknown dungeon key: {snapshot.Header.DungeonKey}");
        _dungeon = dungeon;
        var movementScene = new PhysicsMovementScene(_dungeonRegistry.GetMovementLayout(snapshot.Header.DungeonKey));
        _battleScene = new BattleScene(dungeon.RelationsResolver, movementScene);
        _intentHub = new BattleIntentHub(_battleScene);
        BuildUnits();
        _battleScene.CurrentPhase = BattlePhase.Running;
    }

    /// <summary>按副本配置与头部信息构建全部单位：玩家按头部 NetId 还原，敌人按副本生成顺序自 FirstEnemyNetId 起对齐。</summary>
    private void BuildUnits() {
        foreach (var player in Snapshot.Header.Players) {
            var config = _unitRegistry.GetByKey(player.UnitConfigKey)
                ?? throw new InvalidDataException($"Replay references unknown unit config: {player.UnitConfigKey}");
            var camps = _dungeon.PlayerCampOptions.FirstOrDefault(o => o.Key == player.CampOptionKey)?.Camps
                ?? throw new InvalidDataException($"Replay camp option '{player.CampOptionKey}' not found in dungeon '{_dungeon.DungeonKey}'.");
            AddUnit(new BattleUnit {
                UnitId = player.NetId,
                UnitName = config.ConfigKey,
                Camps = camps,
                Skills = config.Skills,
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
                Position = new Vector2(player.SpawnX, player.SpawnY),
            });
        }

        ushort enemyNetId = Snapshot.Header.FirstEnemyNetId;
        foreach (var spawn in _dungeon.Enemies) {
            var config = _unitRegistry.GetByConfig(spawn.Unit)
                ?? throw new InvalidDataException($"Dungeon '{_dungeon.DungeonKey}' references unregistered unit config.");
            for (int i = 0; i < spawn.Count; i++) {
                var pos = new Vector2(spawn.SpawnBaseX + i * spawn.SpawnXSpacing, 0);
                AddUnit(new BattleUnit {
                    UnitId = enemyNetId++,
                    UnitName = config.ConfigKey,
                    Camps = _dungeon.EnemyCamps,
                    Skills = config.Skills,
                    Intelligence = config.Intelligence,
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
                    Position = pos,
                });
            }
        }
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

    /// <summary>回放记录覆盖的总逻辑帧数（从战斗开始到最后一条输入）。</summary>
    public int TotalFrames {
        get {
            int lastFrame = _startTick;
            if (_moves.Count > 0)
                lastFrame = Math.Max(lastFrame, _moves[^1].Frame);
            if (_casts.Count > 0)
                lastFrame = Math.Max(lastFrame, _casts[^1].Frame);
            if (_focuses.Count > 0)
                lastFrame = Math.Max(lastFrame, _focuses[^1].Frame);
            return Math.Max(0, lastFrame - _startTick + 1);
        }
    }

    /// <summary>
    /// 按帧注入玩家命令：施法 → 移动 → 聚焦，三类共享同一帧轴，经与在线同一个输入门面提交。
    /// 施法与移动都只登记意图，同序要求见 <see cref="BattleIntentHub.PrepareTick"/>；<c>Accepted=false</c> 的条目跳过。
    /// </summary>
    private void InjectInputs() {
        while (_castCursor < _casts.Count) {
            var c = _casts[_castCursor];
            int targetFrame = c.Frame - _startTick;
            if (targetFrame > _frame)
                break;
            if (targetFrame == _frame && c.Accepted)
                _intentHub.Submit(c.ToCommand(UnitIdOf(c.PlayerIndex)));
            _castCursor++;
        }

        while (_moveCursor < _moves.Count) {
            var m = _moves[_moveCursor];
            int targetFrame = m.Frame - _startTick;
            if (targetFrame > _frame)
                break;
            if (targetFrame == _frame)
                _intentHub.Submit(m.ToCommand(UnitIdOf(m.PlayerIndex)));
            _moveCursor++;
        }

        while (_focusCursor < _focuses.Count) {
            var f = _focuses[_focusCursor];
            int targetFrame = f.Frame - _startTick;
            if (targetFrame > _frame)
                break;
            if (targetFrame == _frame && f.Accepted)
                _intentHub.Submit(f.ToCommand(UnitIdOf(f.PlayerIndex)));
            _focusCursor++;
        }
    }

    /// <summary>玩家序号 → 头部玩家表里的单位 ID；越界返回 <see cref="UnitId.None"/>，门内解析不到即自然落空。</summary>
    private UnitId UnitIdOf(byte playerIndex) =>
        playerIndex < _playerUnitIdByIndex.Length ? _playerUnitIdByIndex[playerIndex] : UnitId.None;

    /// <summary>重置到战斗开始帧：先经门面丢弃持旧单位引用的在架意图，再重建战斗世界与单位。</summary>
    private void Reset() {
        _moveCursor = 0;
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

