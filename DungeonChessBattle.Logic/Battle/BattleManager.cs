namespace DungeonChessBattle.Logic.Battle;

public enum BattlePhase
{
    Waiting,
    PlayerTurn,
    SkillCasting,
    Settlement,
    Finished,
}

public class BattleManager
{
    public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Waiting;
    public int RoundNumber { get; private set; }

    public void StartBattle()
    {
        RoundNumber = 1;
        CurrentPhase = BattlePhase.PlayerTurn;
    }

    public void AdvancePhase()
    {
        CurrentPhase = CurrentPhase switch
        {
            BattlePhase.PlayerTurn => BattlePhase.SkillCasting,
            BattlePhase.SkillCasting => BattlePhase.Settlement,
            BattlePhase.Settlement => BattlePhase.PlayerTurn,
            _ => CurrentPhase,
        };
    }

    public void NextRound()
    {
        RoundNumber++;
        CurrentPhase = BattlePhase.PlayerTurn;
    }

    public void EndBattle()
    {
        CurrentPhase = BattlePhase.Finished;
    }
}