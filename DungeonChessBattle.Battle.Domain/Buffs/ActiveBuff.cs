namespace DungeonChessBattle.Battle.Domain.Buffs;

/// <summary>运行时 Buff：实例与效果策略的配对，服务端权威状态。</summary>
public sealed record ActiveBuff(BuffInstance Instance, IBuffEffect Effect);
