using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Models;
using DungeonChessBattle.Protocol.Replay;

namespace DungeonChessBattle.Client.Replay;

/// <summary>
/// 客户端本地回放引擎：解码后的回放快照在本地用战斗世界确定性重跑。
/// 与在线端共用同一 BattleScene，移动经本地位移结算（等价服务端 UnitPawn.Update），
/// 玩家输入按记录帧注入，事件流逐帧返回，单位状态经投影器输出到 <see cref="ReplayUnitView"/>。
/// 纯本地零网络依赖，Godot 主线程逐帧驱动。
/// </summary>
public sealed class ReplayEngine {
    private readonly BattleScene _battleScene;
    private readonly DungeonConfig _dungeon;
    private readonly Dictionary<ushort, ReplayUnitView> _unitViews = [];
    private readonly List<MoveInputRecord> _moves;
    private readonly List<CastSkillRecord> _casts;
    private readonly List<FocusTargetRecord> _focuses;
    private readonly ushort[] _playerNetIdByIndex;
    private readonly int _startTick;
    private readonly float _dt;

    private int _moveCursor;
    private int _castCursor;
    private int _focusCursor;
    private int _frame;
    private readonly Dictionary<ushort, ushort> _focusByNetId = [];

    /// <summary>解码后的回放记录。</summary>
    public ReplayRecordSnapshot Snapshot {
        get;
    }

    /// <summary>当前逻辑帧，战斗开始后第 N tick。</summary>
    public int Frame => _frame;

    /// <summary>战斗是否已结束。</summary>
    public bool IsFinished => _battleScene.IsFinished;

    /// <summary>回放单位展示模型，网络 ID 索引，只读消费。</summary>
    public IReadOnlyDictionary<ushort, ReplayUnitView> UnitViews => _unitViews;

    /// <summary>固定逻辑步长秒数。</summary>
    public float FixedDelta => _dt;

    /// <summary>
    /// 构建回放：解析头部构建战斗世界与单位（玩家按记录、敌人按副本配置从 NextNetId 对齐），
    /// 并立即开始战斗。配置缺失属录制环境不一致，响亮失败。
    /// </summary>
    public ReplayEngine(ReplayRecordSnapshot snapshot) {
        Snapshot = snapshot;
        _startTick = snapshot.Header.StartTick;
        _dt = 1f / snapshot.Header.TickRate;
        _moves = [.. snapshot.MoveInputs.OrderBy(m => m.Frame)];
        _casts = [.. snapshot.CastSkills.OrderBy(c => c.Frame)];
        _focuses = [.. snapshot.FocusTargets.OrderBy(f => f.Frame)];
        _playerNetIdByIndex = [.. snapshot.Header.Players.Select(p => p.NetId)];

        var dungeon = DungeonRegistry.Instance.GetByKey(snapshot.Header.DungeonKey)
            ?? throw new InvalidDataException($"Replay references unknown dungeon key: {snapshot.Header.DungeonKey}");
        _dungeon = dungeon;
        var movementScene = new PhysicsMovementScene(DungeonRegistry.Instance.GetMovementLayout(snapshot.Header.DungeonKey));
        _battleScene = new BattleScene(dungeon.RelationsResolver, movementScene,
            projector: new ReplayProjector(_unitViews));
        BuildUnits();
        _battleScene.StartBattle();
    }

    /// <summary>按副本配置与头部信息构建全部单位：玩家按 PlayerIndex 还原，敌人按副本生成顺序从 NextNetId 对齐。</summary>
    private void BuildUnits() {
        foreach (var player in Snapshot.Header.Players) {
            var config = UnitRegistry.Instance.GetByKey(player.UnitConfigKey)
                ?? throw new InvalidDataException($"Replay references unknown unit config: {player.UnitConfigKey}");
            var camps = _dungeon.PlayerCampOptions.FirstOrDefault(o => o.Key == player.CampOptionKey)?.Camps
                ?? throw new InvalidDataException($"Replay camp option '{player.CampOptionKey}' not found in dungeon '{_dungeon.DungeonKey}'.");
            AddUnit(new BattleUnit {
                UnitNetId = player.NetId,
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

        ushort nextNetId = Snapshot.Header.NextNetId;
        foreach (var spawn in _dungeon.Enemies) {
            var config = UnitRegistry.Instance.GetByConfig(spawn.Unit)
                ?? throw new InvalidDataException($"Dungeon '{_dungeon.DungeonKey}' references unregistered unit config.");
            for (int i = 0; i < spawn.Count; i++) {
                var pos = new Vector2(spawn.SpawnBaseX + i * spawn.SpawnXSpacing, 0);
                AddUnit(new BattleUnit {
                    UnitNetId = nextNetId++,
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

    /// <summary>注册领域单位并创建对应展示模型。</summary>
    private void AddUnit(BattleUnit unit) {
        _battleScene.AddUnit(unit);
        _unitViews[unit.UnitNetId] = new ReplayUnitView {
            NetId = unit.UnitNetId,
            UnitName = unit.UnitName,
            Camps = unit.Camps,
        };
    }

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

        _battleScene.ApplyDecisions();
        ResolveMovement();
        InjectInputs();
        var events = _battleScene.Tick(_dt);
        ApplyFocusViews();
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

    /// <summary>本地位移结算，等价服务端 UnitPawn.Update：MoveResolver + 物理场景。</summary>
    private void ResolveMovement() {
        foreach (var unit in _battleScene.BattleUnits) {
            if (unit.Health <= 0f)
                continue;
            if (unit.MoveInput.LengthSquared() <= 0.0001f || unit.BaseSpeed <= 0f)
                continue;
            unit.Position = MovementResolver.Move(unit.Position, unit.MoveInput, unit.BaseSpeed,
                _dt, unit.BodyRadius, _battleScene.MovementScene, unit.UnitNetId);
            var dir = Vector2.Normalize(unit.MoveInput);
            if (unit.Direction != dir)
                unit.Direction = dir;
        }
    }

    /// <summary>按帧注入玩家输入：移动、施法（仅接受）与聚焦，三类共享同一帧轴。</summary>
    private void InjectInputs() {
        // 移动输入
        while (_moveCursor < _moves.Count) {
            var m = _moves[_moveCursor];
            int targetFrame = m.Frame - _startTick;
            if (targetFrame > _frame)
                break;
            if (targetFrame == _frame && m.PlayerIndex < _playerNetIdByIndex.Length)
                _battleScene.SubmitMove(_playerNetIdByIndex[m.PlayerIndex], new Vector2(m.MoveX, m.MoveY));
            _moveCursor++;
        }

        // 施法请求：服务端拒绝的记录（Accepted=false）跳过，以服务端权威校验为准
        while (_castCursor < _casts.Count) {
            var c = _casts[_castCursor];
            int targetFrame = c.Frame - _startTick;
            if (targetFrame > _frame)
                break;
            if (targetFrame == _frame && c.Accepted && c.PlayerIndex < _playerNetIdByIndex.Length)
                TryCast(_playerNetIdByIndex[c.PlayerIndex], c);
            _castCursor++;
        }

        // 聚焦目标：仅影响展示，不经战斗世界
        while (_focusCursor < _focuses.Count) {
            var f = _focuses[_focusCursor];
            int targetFrame = f.Frame - _startTick;
            if (targetFrame > _frame)
                break;
            if (targetFrame == _frame && f.Accepted && f.PlayerIndex < _playerNetIdByIndex.Length)
                _focusByNetId[_playerNetIdByIndex[f.PlayerIndex]] = f.TargetNetId;
            _focusCursor++;
        }
    }

    /// <summary>按记录载荷发起施法；目标解析失败时静默跳过，不中断回放。</summary>
    private void TryCast(ushort casterNetId, CastSkillRecord record) {
        if (_battleScene.FindUnit(casterNetId) is not BattleUnit caster)
            return;
        BattleUnit? target = null;
        Vector2? targetPos = null;
        if (record.TargetNetId != 0) {
            if (_battleScene.FindUnit(record.TargetNetId) is BattleUnit targetUnit)
                target = targetUnit;
        }
        else {
            targetPos = new Vector2(record.TargetPosX, record.TargetPosZ);
        }
        _battleScene.BeginCast(caster, new SkillKeyId(record.SkillTypeId), target, targetPos);
    }

    /// <summary>把聚焦映射应用到展示模型。</summary>
    private void ApplyFocusViews() {
        foreach (var (netId, view) in _unitViews)
            view.FocusTargetNetId = _focusByNetId.GetValueOrDefault(netId);
    }

    /// <summary>重置到战斗开始帧：重建单位视图与焦点映射。</summary>
    private void Reset() {
        _moveCursor = 0;
        _castCursor = 0;
        _focusCursor = 0;
        _frame = 0;
        _focusByNetId.Clear();
        _unitViews.Clear();
        BuildUnits();
        _battleScene.StartBattle();
    }
}

