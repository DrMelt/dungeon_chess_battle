using System.Numerics;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.GameConfig;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 客户端战斗状态镜像：把在线网络状态（UnitPawn SyncVar）映射为本地 <see cref="IUnitUiView"/> 集合，
/// UI 统一按本镜像取数，与回放重放的 BattleUnit 展示口径一致。
/// 仅做状态落点，不驱动战斗逻辑；阶段与聚焦独立维护。
/// </summary>
public sealed class RoomBattleStateMirror {
    private readonly List<MirrorUnit> _units = [];
    private readonly Dictionary<ushort, MirrorUnit> _byId = [];
    private readonly Dictionary<ushort, ushort> _focusByNetId = [];
    private BattlePhase _phase = BattlePhase.Waiting;
    private ushort _localNetId;

    /// <summary>全部单位只读展示视图。返回内部列表引用，变更统一在主线程网络更新阶段发生，调用方仅允许枚举。</summary>
    public IReadOnlyList<IUnitUiView> Units => _units;

    /// <summary>当前战斗阶段，来自服务器权威载体。</summary>
    public BattlePhase Phase => _phase;

    /// <summary>聚焦映射：单位网络 ID → 目标网络 ID，0 表示无聚焦目标。</summary>
    public IReadOnlyDictionary<ushort, ushort> FocusByNetId => _focusByNetId;

    /// <summary>按网络 ID 查询单位展示视图，不存在返回 null。</summary>
    public IUnitUiView? FindUnit(ushort netId) => _byId.GetValueOrDefault(netId);

    /// <summary>按网络 ID 查询施法判定视图（权威位置），不存在返回 null。</summary>
    public ISkillCasterView? FindCaster(ushort netId) => _byId.GetValueOrDefault(netId);

    /// <summary>本地玩家单位展示视图，未就绪返回 null。</summary>
    public IUnitUiView? LocalUnit => FindUnit(_localNetId);

    /// <summary>本地玩家单位施法判定视图（权威位置），未就绪返回 null。</summary>
    public ISkillCasterView? LocalCaster => FindCaster(_localNetId);

    /// <summary>本地玩家聚焦目标单位展示视图，焦点为 0 或无目标返回 null。</summary>
    public IUnitUiView? LocalFocusUnit {
        get {
            ushort target = _focusByNetId.GetValueOrDefault(_localNetId);
            return target == 0 ? null : FindUnit(target);
        }
    }

    /// <summary>写入服务器权威阶段。</summary>
    public void SetPhase(BattlePhase phase) => _phase = phase;

    /// <summary>记录本地玩家单位网络 ID。</summary>
    public void SetLocalUnit(ushort netId) => _localNetId = netId;

    /// <summary>
    /// 用单位 Pawn 同步本帧状态：缺失单位先创建骨架，再投射可变字段并更新聚焦。
    /// endTickToRemaining 由调用方提供（客户端 ServerTick 换算），解耦镜像与网络计时。
    /// </summary>
    public void SyncFromPawn(UnitPawn pawn, Func<ushort, float> endTickToRemaining) {
        if (!_byId.TryGetValue(pawn.Id, out var unit)) {
            IReadOnlyList<SkillDefinition>? configSkills = UnitRegistry.Instance.GetByKey(pawn.UnitName.Value)?.Skills;
            unit = new MirrorUnit(pawn.Id, pawn.UnitName.Value, pawn.CampTags, configSkills ?? pawn.Skills);
            _units.Add(unit);
            _byId[pawn.Id] = unit;
        }
        unit.SyncFromPawn(pawn, endTickToRemaining);
        _focusByNetId[pawn.Id] = pawn.FocusTargetNetId.Value;
    }

    /// <summary>清空镜像状态，房间会话重置时调用。</summary>
    public void Clear() {
        _units.Clear();
        _byId.Clear();
        _focusByNetId.Clear();
        _phase = BattlePhase.Waiting;
        _localNetId = 0;
    }

    /// <summary>镜像单位：可变展示视图，支持从 UnitPawn 增量改建。同时以实现 <see cref="ISkillCasterView"/> 角色供客户端施法预判取权威位置。</summary>
    private sealed class MirrorUnit(ushort netId, string unitName, IReadOnlyList<string> camps, IReadOnlyList<SkillDefinition> skills)
        : IUnitUiView, ISkillCasterView {
        private readonly List<MirrorBuff> _buffs = [];
        private readonly List<MirrorCooldown> _cooldowns = [];
        private Vector2 _authorityPosition;

        public ushort UnitNetId { get; } = netId;

        public string UnitName { get; } = unitName;

        public IReadOnlyList<string> Camps { get; } = camps;

        public IReadOnlyList<SkillDefinition> Skills { get; } = skills;

        /// <summary>展示位置（渲染插值），供 UI 直读。</summary>
        public Vector2 Position {
            get; set;
        }

        /// <inheritdoc />
        Vector2 IWorldPoseView.Position => _authorityPosition;

        public Vector2 Direction {
            get; set;
        }

        public float Health {
            get; set;
        }

        public float MaxHealth {
            get; set;
        }

        public SkillKeyId SkillCasting {
            get; set;
        }

        public float SkillCastRemaining {
            get; set;
        }

        public float GcdRemaining {
            get; set;
        }

        public float BodyRadius {
            get; set;
        }

        public IReadOnlyList<IBuffUiView> Buffs => _buffs;

        /// <inheritdoc />
        public bool HasSkill(SkillKeyId skillKey) {
            foreach (var skill in Skills)
                if (skill.SkillId == skillKey)
                    return true;
            return false;
        }

        /// <inheritdoc />
        public SkillDefinition? GetSkill(SkillKeyId skillKey) {
            foreach (var skill in Skills)
                if (skill.SkillId == skillKey)
                    return skill;
            return null;
        }

        /// <inheritdoc />
        public float GetTotalCooldownRemaining(SkillKeyId skill) {
            float remaining = GcdRemaining;
            foreach (var cd in _cooldowns)
                if (cd.SkillKey == skill && cd.Remaining > remaining)
                    remaining = cd.Remaining;
            return remaining;
        }

        public void SyncFromPawn(UnitPawn pawn, Func<ushort, float> endTickToRemaining) {
            // 展示位置与朝向取插值值：渲染观感平滑；_authorityPosition 取逻辑值供施法预判。
            Position = pawn.Position.InterpolatedValue;
            _authorityPosition = pawn.Position.Value;
            Direction = pawn.Direction.InterpolatedValue;
            Health = pawn.Health.Value;
            MaxHealth = pawn.MaxHealth.Value;
            BodyRadius = pawn.BodyRadius.Value;
            string casting = pawn.SkillCasting.Value;
            SkillCasting = string.IsNullOrEmpty(casting) ? default : new SkillKeyId(casting);
            SkillCastRemaining = pawn.SkillCastRemaining.Value;
            GcdRemaining = endTickToRemaining(pawn.GcdEndServerTick.Value);
            SyncBuffs(pawn.BuffsList, endTickToRemaining);
            SyncCooldowns(pawn.SkillCooldowns.Value, endTickToRemaining);
        }

        private void SyncBuffs(SyncList<SyncBuffData> buffs, Func<ushort, float> endTickToRemaining) {
            // 原地改建：同位置 Buff 复用对象避免每帧重建分配，字段每帧全量覆盖保证一致。
            int i = 0;
            foreach (var b in buffs) {
                if (i >= _buffs.Count)
                    _buffs.Add(new MirrorBuff());
                _buffs[i++].Update(b, endTickToRemaining);
            }
            if (i < _buffs.Count)
                _buffs.RemoveRange(i, _buffs.Count - i);
        }

        private void SyncCooldowns(SyncSkillCooldownSnapshot? snapshot, Func<ushort, float> endTickToRemaining) {
            if (snapshot == null) {
                _cooldowns.Clear();
                return;
            }
            // 原地改建：同位置冷却项复用对象避免每帧重建分配，字段全量覆盖保证一致。
            int i = 0;
            foreach (var entry in snapshot.Entries) {
                if (i >= _cooldowns.Count)
                    _cooldowns.Add(new MirrorCooldown());
                _cooldowns[i].SkillKey = new SkillKeyId(entry.SkillId);
                _cooldowns[i].Remaining = endTickToRemaining(entry.EndServerTick);
                i++;
            }
            if (i < _cooldowns.Count)
                _cooldowns.RemoveRange(i, _cooldowns.Count - i);
        }
    }

    /// <summary>镜像 Buff 展示：可变，字段与网络 SyncBuffData 对齐。</summary>
    private sealed class MirrorBuff : IBuffUiView {
        public ushort BuffTypeId {
            get;
            set;
        }

        public int Stacks {
            get;
            set;
        }

        public int MaxStacks {
            get;
            set;
        }

        public double Remaining {
            get;
            set;
        }

        public ushort FromNetId {
            get;
            set;
        }

        public DamageType DamageType {
            get;
            set;
        }

        public void Update(SyncBuffData b, Func<ushort, float> endTickToRemaining) {
            BuffTypeId = b.BuffTypeId;
            Stacks = b.StackCount;
            MaxStacks = b.MaxStackCount;
            Remaining = endTickToRemaining(b.EndServerTick);
            FromNetId = b.SourceUnitNetId;
            DamageType = (DamageType)b.DamageType;
        }
    }

    /// <summary>镜像冷却项：技能键与剩余秒数。</summary>
    private sealed class MirrorCooldown {
        public SkillKeyId SkillKey;
        public float Remaining;
    }
}
