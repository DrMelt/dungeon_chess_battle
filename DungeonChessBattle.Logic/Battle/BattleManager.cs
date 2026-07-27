namespace DungeonChessBattle.Logic.Battle;

public enum BattlePhase {
    Waiting,
    PlayerTurn,
    SkillCasting,
    Finished,
}

public class BattleManager {
    public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Waiting;
    public int RoundNumber { get; private set; }

    public event Action<BattlePhase, BattlePhase>? PhaseChanged;
    public event Action<int>? RoundStarted;

    public void StartBattle() {
        RoundNumber = 1;
        TransitionTo(BattlePhase.PlayerTurn);
        RoundStarted?.Invoke(RoundNumber);
    }

    /// <summary>
    /// 推进到下个阶段。SkillCasting 后自动进入新回合的 PlayerTurn。
    /// </summary>
    public void Advance() {
        var next = CurrentPhase switch {
            BattlePhase.PlayerTurn => BattlePhase.SkillCasting,
            BattlePhase.SkillCasting => BattlePhase.PlayerTurn,
            _ => CurrentPhase,
        };

        // FIXME: [AI/2025-07-27] 当没有技能施放时不应进入 SkillCasting，当前未做守卫。
        // 后续需要根据回合内是否提交过技能来决定是否跳过 SkillCasting 直接进入下回合。

        if (next == BattlePhase.PlayerTurn) {
            RoundNumber++;
            RoundStarted?.Invoke(RoundNumber);
        }

        TransitionTo(next);
    }

    public void EndBattle() {
        TransitionTo(BattlePhase.Finished);
    }

    private void TransitionTo(BattlePhase next) {
        if (CurrentPhase == next)
            return;
        var prev = CurrentPhase;
        CurrentPhase = next;
        PhaseChanged?.Invoke(prev, next);
    }
}
