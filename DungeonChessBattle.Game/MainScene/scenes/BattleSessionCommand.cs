using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Game.GamePlayUI.skill_list;

namespace DungeonChessBattle.Game.MainScene.scenes;

/// <summary>
/// 战斗会话玩家命令契约：聚焦与施法的写侧入口。
/// 继承 <see cref="ISkillCaster"/> 使技能预输入缓冲直接消费命令，UI 不接触
/// <see cref="IClientBattleService"/> 与房间 ID，门面边界不被绕过。
/// </summary>
public interface IBattleSessionCommand : ISkillCaster {
    /// <summary>是否已绑定会话。</summary>
    bool IsInBattle {
        get;
    }

    /// <summary>请求本地玩家单位设置聚焦目标，0 表示清除。</summary>
    void SetLocalFocusTarget(ushort targetNetId);
}

/// <summary>
/// 战斗会话命令实现：把聚焦/施法意图经 <see cref="IClientBattleService"/> 发送，服务端权威校验。
/// 施法者由服务端从请求来源控制器推导，客户端不指定；本地单位或服务未就绪时不发起。
/// </summary>
public sealed class BattleSessionCommand : IBattleSessionCommand {
    /// <summary>在线战斗会话（Bind 时注入，承担命令转发与本地单位 ID 读取）。</summary>
    private IClientBattleSession? _session;

    /// <summary>当前房间 ID（供 RPC 上下文标识）。</summary>
    private string _roomId = "";

    /// <inheritdoc />
    public bool IsInBattle => _session != null;

    /// <summary>进入战斗：注入在线战斗会话与房间 ID。</summary>
    public void Bind(IClientBattleSession session, string roomId) {
        _session = session;
        _roomId = roomId;
    }

    /// <summary>退出战斗：释放会话引用与房间 ID。</summary>
    public void Unbind() {
        _session = null;
        _roomId = "";
    }

    /// <inheritdoc />
    public void SetLocalFocusTarget(ushort targetNetId) {
        var session = _session;
        var localUnitNetId = session?.LocalUnit?.UnitId ?? 0;
        if (session == null || localUnitNetId == 0)
            return;
        session.SetFocusTarget(_roomId, localUnitNetId, targetNetId);
    }

    /// <inheritdoc />
    public void Cast(SkillKeyId skillKey, ushort targetNetId, float posX, float posZ) {
        var session = _session;
        var localUnitNetId = session?.LocalUnit?.UnitId ?? 0;
        if (session == null || localUnitNetId == 0)
            return;
        session.CastSkill(_roomId, localUnitNetId, targetNetId, skillKey.Id, posX, posZ);
    }
}
