using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 单位的网络同步 Entity。纯数据载体，由服务端直接操作 SyncVar/SyncList。
/// </summary>
public class UnitSyncEntity : EntityLogic {
    private static RemoteCallSerializable<SyncSkillRequest> CastSkillRPC;

    public readonly SyncString UnitName = new();
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

    public event Action<UnitSyncEntity, float, float>? HealthChanged;
    public event Action<UnitSyncEntity>? UnitDied;
    public event Action<UnitSyncEntity, SyncBuffData>? BuffAdded;
    public event Action<UnitSyncEntity, SyncBuffData>? BuffRemoved;

    /// <summary>
    /// 客户端发起技能请求时触发。回调参数为当前服务端实例和请求数据。
    /// </summary>
    public static event Action<UnitSyncEntity, SyncSkillRequest>? SkillCastRequested;

    public UnitSyncEntity(EntityParams entityParams) : base(entityParams) { }

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
        r.CreateRPCAction<UnitSyncEntity, SyncSkillRequest>(
            (e, req) => e.OnRpcCastSkill(req),
            ref CastSkillRPC,
            ExecuteFlags.ExecuteOnServer);
    }

    private void OnRpcCastSkill(SyncSkillRequest req) {
        SkillCastRequested?.Invoke(this, req);
    }

    /// <summary>
    /// 客户端调用，发起技能施放 RPC 到服务端。
    /// </summary>
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
}
