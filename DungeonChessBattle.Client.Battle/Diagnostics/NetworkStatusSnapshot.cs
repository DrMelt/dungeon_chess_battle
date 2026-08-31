using LiteEntitySystem;

namespace DungeonChessBattle.Client.Battle.Diagnostics;

/// <summary>
/// 传输层通用指标，房间链路，往返与单向延迟、每秒收发、丢包与出站可靠队列积压。
/// LiteNetLib 的单向值即往返半值估计，不是独立测量。丢包率自连接起累计、整型截断，
/// 出站可靠队列深度为瞬时值，计的是本端等待对端确认的有序可靠包。
/// 出站为应用层每次发送调用的近似值，1 包加字节长度，非原始 UDP 报文。
/// </summary>
public readonly record struct TransportMetrics(
    int RttMs, int OneWayMs,
    int PacketsInPerSecond, long BytesInPerSecond,
    int PacketsOutPerSecond, long BytesOutPerSecond,
    long PacketLossPercent, int OutgoingReliableQueue);

/// <summary>
/// LES 实体同步的原始读数，全部为 <c>ClientEntityManager</c> 直读值，构造方不做任何加工。
/// tick 有两套起点：LocalTick/SrvAckTick/SrvRecvTick 随客户端本地计数，服务端只回显后两者；
/// ServerTick/SrvStateTickA/SrvStateTickB 随服务端计数。跨套相减无意义。
/// SrvAckTick/SrvRecvTick 只随 diff 状态下发、baseline 不带，未收到时为 0。
/// StateSize 是状态 A 解压后的字节数，不是报文长度；Tickrate 随 baseline 跟随服务端，
/// StateSendIntervalTicks 是服务端两次状态下发之间的 tick 间隔。
/// 对照 LES 原名：LocalTick=Tick、SrvAckTick=LastProcessedTick、SrvRecvTick=LastReceivedTick、
/// StateSendIntervalTicks=ServerSendRate。派生读数由本类型自带，取用前先看 <see cref="TickLagTrusted"/>。
/// LES 的 jitter 读数名义为毫秒、实际单位为秒，此处已按秒命名。
/// <see cref="StateSpreadAvg"/> 与 <see cref="StateSpreadMax"/> 例外：它们是生产侧每帧采样、
/// 每秒结算的窗口统计，不是本帧直读值。
/// </summary>
public sealed record BattleEntityMetrics(
    byte Tickrate,
    int StateSendIntervalTicks,
    ushort LocalTick,
    ushort SrvAckTick,
    ushort SrvRecvTick,
    ushort ServerTick,
    ushort SrvStateTickA,
    ushort SrvStateTickB,
    int StoredCommands,
    ushort EntitiesCount,
    byte ServerInputBuffer,
    int LerpBufferCount,
    float LerpBufferTimeSeconds,
    float JitterMaxSeconds,
    float JitterAvgSeconds,
    int StateSize,
    int PendingToRemoveEntities,
    float StateSpreadAvg,
    int StateSpreadMax) {
    /// <summary>单个 tick 的毫秒宽度，<see cref="Tickrate"/> 未定时为 0。</summary>
    public float TickMs => Tickrate == 0 ? 0f : 1000f / Tickrate;

    /// <summary>上行在途：本地已生成、服务端尚未收到的输入 tick 跨度。</summary>
    public int UplinkTicks => Utils.SequenceDiff(LocalTick, SrvRecvTick);

    /// <summary>服务端排队：已收到但尚未消化的输入 tick 跨度。</summary>
    public int ServerQueueTicks => Utils.SequenceDiff(SrvRecvTick, SrvAckTick);

    /// <summary>本地预测领先权威确认的 tick 跨度，即 LocalTick 减 SrvAckTick。</summary>
    public int AckLagTicks => UplinkTicks + ServerQueueTicks;

    /// <summary>
    /// 当前插值节拍乘数：正在播的 A 与目标 B 之间的服务端 tick 差。正常恒为 1，
    /// 大于 1 说明下行状态跳号，LES 的 <c>_remoteInterpolationTotalTime</c> 按此倍数放大，
    /// 消费速率随之跌到 128/倍 每秒，缓冲开始积压。
    /// </summary>
    public int StateSpreadTicks => Utils.SequenceDiff(SrvStateTickB, SrvStateTickA);

    /// <summary>播放欠账：已收到但尚未播出的状态覆盖多少服务端 tick。</summary>
    public int PlaybackDebtTicks => LerpBufferCount * StateSendIntervalTicks;

    /// <summary>
    /// 净权威回环：从 <see cref="AckLagTicks"/> 里剥掉播放欠账，剩下的才是上行与服务端耗时。
    /// <see cref="AckLagTicks"/> 取自 state A 的回显，A 本身可能已积压，故两者必须分开读。
    /// 不可信或欠账估计偏大时钳到 0。
    /// </summary>
    public int NetAckLagTicks {
        get {
            if (!TickLagTrusted)
                return 0;
            int net = AckLagTicks - PlaybackDebtTicks;
            return net > 0 ? net : 0;
        }
    }

    /// <summary><see cref="NetAckLagTicks"/> 的毫秒读数。</summary>
    public float NetAckLagMs => NetAckLagTicks * TickMs;

    /// <summary>三段分解是否可信：回显到位、无倒挂、累计不超一秒。为假时三段读数无意义。</summary>
    public bool TickLagTrusted {
        get {
            if (Tickrate == 0 || SrvAckTick == 0 || SrvRecvTick == 0)
                return false;
            int uplink = UplinkTicks;
            int serverQueue = ServerQueueTicks;
            return uplink >= 0 && serverQueue >= 0 && uplink + serverQueue <= Tickrate;
        }
    }

    /// <summary><see cref="AckLagTicks"/> 的毫秒读数，不可信时为 0。</summary>
    public float AckLagMs => TickLagTrusted ? AckLagTicks * TickMs : 0f;
}

/// <summary>
/// 对外唯一网络状态快照契约。消费方，如后续调试 UI，只依赖本类型。
/// </summary>
public sealed record NetworkStatusSnapshot(
    bool IsConnected, string Host, int Port,
    TransportMetrics Transport, BattleEntityMetrics? Entity);
