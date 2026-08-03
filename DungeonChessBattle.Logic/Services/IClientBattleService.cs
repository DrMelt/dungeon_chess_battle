using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// 客户端战斗服务接口，仅包含客户端需要的查询与操作。
/// 所有方法使用 roomId 作为上下文标识，不暴露 BattleManager。
/// </summary>
public interface IClientBattleService {
    // ── 事件（UI 层订阅） ──
    /// <summary>单位创建事件。参数：房间ID、单位名称、阵营(byte)</summary>
    event Action<string, string, byte>? OnUnitCreated;

    /// <summary>战斗阶段变化事件。参数：房间ID、战斗阶段</summary>
    event Action<string, BattlePhase>? BattlePhaseChanged;

    /// <summary>单位生命值变化事件。参数：单位名称、新生命值、旧生命值</summary>
    event Action<string, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位名称</summary>
    event Action<string>? UnitDied;

    /// <summary>单位添加 Buff 事件。参数：单位名称、Buff 数据</summary>
    event Action<string, BuffEventData>? UnitBuffAdded;

    /// <summary>单位移除 Buff 事件。参数：单位名称、Buff 数据</summary>
    event Action<string, BuffEventData>? UnitBuffRemoved;

    // 房间管理
    GameRoom? GetRoom(string roomId);
    IEnumerable<GameRoom> GetAllRooms();
    GameRoom CreateRoom(string roomId);
    IUnitState CreateUnit(string roomId, string unitName, byte camp);

    // 技能（客户端发起）
    void CastSkill(string roomId, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null);

    // Buff 更新（服务端权威结算后下推；本地模式由 Logic 层直接处理）
    void UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime);

    // 胜负判定
    bool CheckBattleEnded(string roomId);

    /// <summary>请求开始战斗。网络模式通过 RPC 发送，本地模式直接调用内部逻辑。</summary>
    void RequestStartBattle(string roomId);

    /// <summary>提交玩家输入。参数展开为 float 避免接口层依赖 System.Numerics。</summary>
    void SubmitPlayerInput(float moveX, float moveY, byte skillFlags, float aimX, float aimY);
}
