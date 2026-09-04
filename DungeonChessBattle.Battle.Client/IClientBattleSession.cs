using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 在线战斗会话契约：房间链路对上层的唯一可见面，由 <see cref="RoomBattleClient"/> 实现。
/// 写侧继承 <see cref="IClientBattleService"/> 的命令与事件，读侧为本契约自持的单位读数
/// （<see cref="Units"/> 与 <see cref="FindUnit"/>），另补本地玩家语义与房间权威元信息。
/// 消费方是 Game 层的表现层数据源投影与战斗编排器，UI 面板不直接持有本契约。
/// 不含连接生命周期——连接一律由客户端门面状态机发起，消费方拿不到传输对象。
/// </summary>
public interface IClientBattleSession : IClientBattleService {
    /// <summary>全部展示单位视图，读本地回填的战斗世界。</summary>
    IReadOnlyList<IUnitUiView> Units {
        get;
    }

    /// <summary>按单位 ID 查展示单位，不存在返回 null。</summary>
    IUnitUiView? FindUnit(UnitId unitId);

    /// <summary>本地玩家单位的展示视图，控制器未就绪时返回 null。</summary>
    IUnitUiView? LocalUnit {
        get;
    }

    /// <summary>本地玩家聚焦目标单位的展示视图，无聚焦目标时返回 null。</summary>
    IUnitUiView? LocalFocus {
        get;
    }

    /// <summary>当前房间副本键，来自服务端权威房间实体同步；实体未同步时为 null。</summary>
    string? DungeonKey {
        get;
    }

    /// <summary>战斗开始时刻，服务端权威 UTC Unix 秒；房间实体未同步时为 null。</summary>
    long? BattleStartUnixTime {
        get;
    }
}
