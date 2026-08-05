using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Logic.Battle;

/// <summary>
/// 实时化战斗管理器。不再有回合概念，由 Tick(deltaTime) 驱动每帧逻辑。
/// PhaseChanged 改为 BattleStarted / BattleEnded 事件。
/// </summary>
public class BattleManager : IBattle {
    /// <summary>当前战斗阶段。</summary>
    public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Waiting;

    /// <summary>战斗已运行的秒数。</summary>
    public float ElapsedTime {
        get; private set;
    }

    /// <summary>战斗开始时触发。</summary>
    public event Action? BattleStarted;

    /// <summary>战斗结束时触发。</summary>
    public event Action? BattleEnded;

    /// <summary>
    /// 开始战斗（从等待/结束阶段切换到进行中并清零计时）。
    /// </summary>
    public void StartBattle() {
        if (CurrentPhase == BattlePhase.Running)
            return;

        ElapsedTime = 0f;
        var prev = CurrentPhase;
        CurrentPhase = BattlePhase.Running;
        if (prev != BattlePhase.Running)
            BattleStarted?.Invoke();
    }

    /// <summary>
    /// 每帧调用，驱动实时战斗逻辑（输入处理由 EntityManager 自动完成）。
    /// 可选的额外逻辑层 tick（技能冷却、范围伤害判定等）。
    /// </summary>
    public void Tick(float deltaTime) {
        if (CurrentPhase != BattlePhase.Running)
            return;
        ElapsedTime += deltaTime;
    }

    /// <summary>
    /// 结束战斗（切换到完成阶段）。
    /// </summary>
    public void EndBattle() {
        if (CurrentPhase == BattlePhase.Finished)
            return;
        var prev = CurrentPhase;
        CurrentPhase = BattlePhase.Finished;
        if (prev != BattlePhase.Finished)
            BattleEnded?.Invoke();
    }
}
