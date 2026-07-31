using System.Numerics;
using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 实时化的单位 Pawn 实体。继承 PawnLogic，支持移动、技能、预测回滚。
/// 逐步替代 UnitSyncEntity（回合制纯数据载体）。
/// </summary>
public class UnitPawn : PawnLogic {
    // ── RPC ──────────────────────────────────────────────
    private static RemoteCallSerializable<SyncSkillRequest> CastSkillRPC;

    // ── SyncVars ─────────────────────────────────────────
    public readonly SyncString UnitName = new();

    public SyncVar<Vector2> Position;
    public SyncVar<float> Rotation;
    public SyncVar<float> Health;
    public SyncVar<float> MaxHealth;
    public SyncVar<byte> Camp;
    public SyncVar<byte> UnitState;
    public SyncVar<float> GcdRemaining;
    public SyncVar<float> PhysicalAttackBase;
    public SyncVar<float> MagicAttackBase;
    public SyncVar<float> PhysicalTakePercent;
    public SyncVar<float> MagicTakePercent;
    public SyncVar<float> CureIntensity;
    public SyncVar<float> BaseSpeed;

    public readonly SyncList<SyncBuffData> BuffsList = [];
    public readonly SyncList<ushort> SkillIds = [];
    public readonly SyncList<SyncHateData> HatesList = [];

    // ── 事件（客户端 UI 层监听） ──────────────────────────
    public event Action<UnitPawn, float, float>? HealthChanged;
    public event Action<UnitPawn>? UnitDied;
    public event Action<UnitPawn, SyncBuffData>? BuffAdded;
    public event Action<UnitPawn, SyncBuffData>? BuffRemoved;

    public static event Action<UnitPawn, SyncSkillRequest>? SkillCastRequested;

    public UnitPawn(EntityParams entityParams) : base(entityParams) { }

    protected override void OnConstructed() {
        Health.Value = 1000f;
        MaxHealth.Value = 1000f;
        Camp.Value = 0;
        UnitState.Value = 0;
        PhysicalAttackBase.Value = 1.0f;
        MagicAttackBase.Value = 1.0f;
        PhysicalTakePercent.Value = 1.0f;
        MagicTakePercent.Value = 1.0f;
        CureIntensity.Value = 1.0f;
        BaseSpeed.Value = 2.0f;
    }

    protected override void RegisterRPC(ref RPCRegistrator r) {
        base.RegisterRPC(ref r);
        r.CreateRPCAction<UnitPawn, SyncSkillRequest>(
            (e, req) => e.OnRpcCastSkill(req),
            ref CastSkillRPC,
            ExecuteFlags.ExecuteOnServer);
    }

    private void OnRpcCastSkill(SyncSkillRequest req) {
        SkillCastRequested?.Invoke(this, req);
    }

    public void RequestCastSkill(SyncSkillRequest req) {
        ExecuteRPC(CastSkillRPC, req);
    }

    public void ServerSetHealth(float newHealth) {
        if (!IsServer)
            return;
        float oldHealth = Health.Value;
        Health.Value = Math.Clamp(newHealth, 0f, MaxHealth.Value);
        if (MathF.Abs(Health.Value - oldHealth) > 0.0001f) {
            HealthChanged?.Invoke(this, Health.Value, oldHealth);
            if (Health.Value <= 0) {
                UnitState.Value = 1;
                UnitDied?.Invoke(this);
            }
        }
    }

    public void ServerAddBuff(SyncBuffData buffData) {
        if (!IsServer)
            return;
        if (buffData.IsStackable) {
            for (int i = 0; i < BuffsList.Count; i++) {
                var existing = BuffsList[i];
                if (existing.BuffTypeId == buffData.BuffTypeId) {
                    existing.StackCount = (ushort)Math.Min(existing.StackCount + 1, existing.MaxStackCount);
                    existing.RemainingDuration = Math.Max(existing.RemainingDuration, buffData.RemainingDuration);
                    BuffsList[i] = existing;
                    return;
                }
            }
        }
        BuffsList.Add(buffData);
        BuffAdded?.Invoke(this, buffData);
    }

    public void ServerRemoveBuffAt(int index) {
        if (!IsServer)
            return;
        if (index < 0 || index >= BuffsList.Count)
            return;
        var removed = BuffsList[index];
        BuffsList.RemoveAt(index);
        BuffRemoved?.Invoke(this, removed);
    }

    public void ServerUpdateBuffDuration(int index, float newRemaining) {
        if (!IsServer)
            return;
        if (index < 0 || index >= BuffsList.Count)
            return;
        var buff = BuffsList[index];
        buff.RemainingDuration = newRemaining;
        BuffsList[index] = buff;
    }

    public void ServerAddHate(ushort targetUnitNetId, float hateValue) {
        if (!IsServer)
            return;
        for (int i = 0; i < HatesList.Count; i++) {
            var existing = HatesList[i];
            if (existing.TargetUnitNetId == targetUnitNetId) {
                existing.HateValue += hateValue;
                HatesList[i] = existing;
                return;
            }
        }
        HatesList.Add(new SyncHateData { TargetUnitNetId = targetUnitNetId, HateValue = hateValue });
    }

    public void ApplyMovement(Vector2 moveDir, float deltaTime) {
        if (moveDir.LengthSquared() > 1f)
            moveDir = Vector2.Normalize(moveDir);

        Position.Value += moveDir * BaseSpeed.Value * deltaTime;
    }

    public void UpdateCooldowns(float deltaTime) {
        if (GcdRemaining.Value > 0)
            GcdRemaining.Value = Math.Max(0, GcdRemaining.Value - deltaTime);
    }
}
