using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class BuffModel : IBuff {
    public string BuffName { get; set; } = "BuffName";
    public string BuffDescription { get; set; } = "BuffDescription";
    public string IconPath { get; set; } = "";

    public double Duration { get; set; } = 60;
    public int Superpositions { get; set; } = 1;
    public int MaxSuperpositions { get; set; } = 1;
    public bool IsAlive { get; set; } = true;

    public IUnitState FromUnit { get; set; } = null!;

    public void Update(double deltaTime, IUnitState unitState) {
        if (!IsAlive)
            return;

        ActionDuration(deltaTime, unitState);

        Duration -= deltaTime;
        if (Duration < 0 || Superpositions <= 0) {
            ActionEnd(unitState);
            IsAlive = false;
        }
    }

    protected virtual void ActionDuration(double deltaTime, IUnitState unitState) {
    }

    protected virtual void ActionEnd(IUnitState unitState) {
    }

    public void AddSuperpositions(IBuff other) {
        Superpositions += 1;
        Superpositions = Math.Min(Superpositions, other.MaxSuperpositions);
        Duration = Math.Max(Duration, other.Duration);
    }
}
