using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Logic.Battle;

/// <summary>
/// 战斗流程抽象接口，屏蔽 <see cref="BattleManager"/> 具体实现。
/// Logic 门面服务对战斗的全部操作面向该接口，避免对外暴露实现类型。
/// </summary>
public interface IBattle {
    /// <summary>当前战斗阶段。</summary>
    BattlePhase CurrentPhase {
        get;
    }

    /// <summary>战斗已运行的秒数。</summary>
    float ElapsedTime {
        get;
    }

    /// <summary>战斗开始时触发。</summary>
    event Action? BattleStarted;

    /// <summary>战斗结束时触发。</summary>
    event Action? BattleEnded;

    /// <summary>开始战斗（从等待/结束阶段切换到进行中并清零计时）。</summary>
    void StartBattle();

    /// <summary>每帧调用，驱动实时战斗逻辑。</summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    void Tick(float deltaTime);

    /// <summary>结束战斗（切换到完成阶段）。</summary>
    void EndBattle();
}
