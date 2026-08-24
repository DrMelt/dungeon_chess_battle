using System;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Client.Battle;

namespace DungeonChessBattle.Game.GamePlayUI.skill_list;

/// <summary>
/// 基于 IClientBattleService 的施放动作实现。
/// 房间、施法者与战斗服务在战斗生命周期内动态变化，故以提供者方式注入。
/// </summary>
/// <remarks>
/// 构造施放动作。
/// </remarks>
/// <param name="serviceProvider">当前战斗服务提供者，未进入战斗时返回 null。</param>
/// <param name="roomIdProvider">当前房间 ID 提供者。</param>
/// <param name="casterNetIdProvider">本地位施法单位网络 ID 提供者。</param>
public sealed class BattleSkillCaster(Func<IClientBattleService?> serviceProvider, Func<string> roomIdProvider, Func<ushort> casterNetIdProvider) : ISkillCaster {
    private readonly Func<IClientBattleService?> _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly Func<string> _roomIdProvider = roomIdProvider ?? throw new ArgumentNullException(nameof(roomIdProvider));
    private readonly Func<ushort> _casterNetIdProvider = casterNetIdProvider ?? throw new ArgumentNullException(nameof(casterNetIdProvider));

    /// <inheritdoc />
    public void Cast(SkillKeyId skillKey, ushort targetNetId, float posX, float posZ) {
        _serviceProvider()?.CastSkill(_roomIdProvider(), _casterNetIdProvider(), targetNetId, skillKey.Id, posX, posZ);
    }
}
