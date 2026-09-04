using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.GamePlayUI.skill_list;

namespace DungeonChessBattle.Game.BattleScene;

/// <summary>
/// 战斗会话玩家命令契约：聚焦与施法的写侧入口。
/// 继承 <see cref="ISkillCaster"/> 使技能面板直接消费命令，UI 不接触
/// <see cref="IClientBattleService"/> 与房间 ID，门面边界不被绕过。
/// 仅在线装配存在；回放无命令可发，契约整体缺席。
/// </summary>
public interface IBattleSessionCommand : ISkillCaster {
    /// <summary>命令装配是否在场。</summary>
    bool IsInBattle {
        get;
    }

    /// <summary>请求本地玩家单位设置聚焦目标，0 表示清除。</summary>
    void SetLocalFocusTarget(ushort targetNetId);
}

/// <summary>
/// 在线命令装配：把聚焦/施法意图经 <see cref="IClientBattleSession"/> 发送，服务端权威校验。
/// 施法者由服务端从请求来源控制器推导，客户端不指定；本地单位未就绪时不发起。
/// </summary>
public sealed class BattleSessionCommand(IClientBattleSession session, string roomId) : IBattleSessionCommand {
    /// <inheritdoc />
    public bool IsInBattle => true;

    /// <inheritdoc />
    public void SetLocalFocusTarget(ushort targetNetId) {
        if (session.LocalUnit is null)
            return;
        session.SetFocusTarget(roomId, targetNetId);
    }

    /// <inheritdoc />
    public void Cast(SkillKeyId skillKey, ushort targetNetId, float posX, float posZ) {
        if (session.LocalUnit is null)
            return;
        session.CastSkill(roomId, targetNetId, skillKey.Id, posX, posZ);
    }
}
