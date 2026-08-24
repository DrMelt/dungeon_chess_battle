namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>运行时 Buff：定义、实例与效果策略的配对，服务端权威状态。</summary>
public sealed record ActiveBuff(BuffInstance Instance, BuffDefinition Definition, IBuffEffect Effect);
