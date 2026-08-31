using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 在线战斗会话契约：房间链路对展示层的唯一可见面，由 <see cref="RoomBattleClient"/> 实现。
/// 读侧继承 <see cref="IBattleViewSource"/> 的展示口径，写侧继承 <see cref="IClientBattleService"/>
/// 的命令与事件，另补本地玩家语义与房间权威元信息。
/// 不含连接生命周期——连接一律由客户端门面状态机发起，消费方拿不到传输对象。
/// 本地玩家成员为在线专属，不入 <see cref="IBattleViewSource"/>，回放侧无本地控制器。
/// </summary>
public interface IClientBattleSession : IClientBattleService, IBattleViewSource {
    /// <summary>本地玩家单位的展示视图，控制器未就绪时返回 null。</summary>
    IUnitUiView? LocalUnit {
        get;
    }

    /// <summary>本地玩家聚焦目标单位的展示视图，无聚焦目标时返回 null。</summary>
    IUnitUiView? LocalFocus {
        get;
    }

    /// <summary>本地玩家单位的施法判定视图，控制器未就绪时返回 null。</summary>
    ISkillCasterView? LocalCaster {
        get;
    }

    /// <summary>按网络 ID 查询施法判定视图，不存在返回 null。</summary>
    /// <param name="netId">单位网络实体 ID。</param>
    ISkillCasterView? FindCaster(ushort netId);

    /// <summary>当前房间副本键，来自服务端权威房间实体同步；实体未同步时为 null。</summary>
    string? DungeonKey {
        get;
    }

    /// <summary>战斗开始时刻，服务端权威 UTC Unix 秒；房间实体未同步时为 null。</summary>
    long? BattleStartUnixTime {
        get;
    }
}
