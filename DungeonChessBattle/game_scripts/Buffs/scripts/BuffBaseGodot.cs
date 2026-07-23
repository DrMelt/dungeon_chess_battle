using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class BuffBaseGodot : Resource, IBuff {
    [Export]
    public string buffName;
    public string BuffName => buffName;

    [Export(PropertyHint.MultilineText)]
    public string buffDescription;
    public string BuffDescription => buffDescription;

    [Export]
    public Texture2D icon;
    public string IconPath => icon?.ResourcePath ?? "";

    [Export]
    public double duration = 60;
    public double Duration => duration;

    [Export]
    public int superpositions = 1;
    public int Superpositions => superpositions;

    [Export]
    public int maxSuperpositions = 1;
    public int MaxSuperpositions => maxSuperpositions;

    [Export]
    public bool isAlive = true;
    public bool IsAlive => isAlive;

    [ExportGroup("Runtime Parameters")]
    public IUnitState fromUnit;
    public IUnitState FromUnit => fromUnit;

    public void Update(double deltaTime, IUnitState unitState) {
        if (!isAlive) {
            return;
        }

        ActionDuration(deltaTime, unitState);

        duration -= deltaTime;
        if (duration < 0 || superpositions <= 0) {
            ActionEnd(unitState);
            isAlive = false;
        }
    }

    protected virtual void ActionDuration(double deltaTime, IUnitState unitState) {
    }

    protected virtual void ActionEnd(IUnitState unitState) {
    }

    public void AddSuperpositions(IBuff other) {
        superpositions += 1;
        superpositions = Mathf.Min(superpositions, other.MaxSuperpositions);
        duration = Mathf.Max(duration, other.Duration);
    }
}
