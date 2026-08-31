using DungeonChessBattle.Battle.Shared.Combat;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 客户端本地战斗循环（LES LocalSingleton）当前只承担展示取数：`Update`/`LateUpdate` 为空实现，
/// 在线端不跑本地结算；`VisualUpdate` 每渲染帧以 <see cref="BattleSceneMirror.Pull"/> 把
/// `UnitPawn` 的 SyncVar 读数回填进本地 `BattleScene`。移动、读条与伤害一律由服务端权威结算。
/// 现状时序与已知偏差见 docs/flow/client-prediction.md。
/// </summary>
internal sealed class ClientBattleLoop(RoomBattleClient owner) : ILocalSingletonWithUpdate {
    private readonly RoomBattleClient _owner = owner;

    /// <summary>LES 逻辑 tick 回调，当前不做本地结算。</summary>
    public void Update(float dt) {
    }

    /// <summary>LES 逻辑 tick 末回调，当前为空。</summary>
    public void LateUpdate(float dt) {
    }

    /// <summary>
    /// 渲染帧取数：把 SyncVar 当前读数覆写进领域位置。
    /// 该钩子在 <c>ClientEntityManager.Update</c> 开头触发，本帧下行 diff 尚未应用。
    /// </summary>
    public void VisualUpdate(float dt) {
        var scene = _owner.BattleScene;
        if (scene is null)
            return;
        BattleSceneMirror.Pull(scene, _owner.PawnByNetId.Values);
    }

    /// <summary>随连接释放，无需显式清理。</summary>
    public void Destroy() {
    }
}
