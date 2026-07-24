using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class BuffBaseGodot : Resource, IBuff {
    protected BuffModel _model = null!;

    [Export]
    public string buffName;
    public string BuffName => _model?.BuffName ?? buffName;

    [Export(PropertyHint.MultilineText)]
    public string buffDescription;
    public string BuffDescription => _model?.BuffDescription ?? buffDescription;

    [Export]
    public Texture2D icon;
    public string IconPath => _model?.IconPath ?? icon?.ResourcePath ?? "";

    [Export]
    public double duration = 60;
    public double Duration => _model?.Duration ?? duration;

    [Export]
    public int superpositions = 1;
    public int Superpositions => _model?.Superpositions ?? superpositions;

    [Export]
    public int maxSuperpositions = 1;
    public int MaxSuperpositions => _model?.MaxSuperpositions ?? maxSuperpositions;

    public bool IsAlive => _model?.IsAlive ?? true;

    public IUnitState FromUnit => _model?.FromUnit!;

    private void EnsureModelCreated() {
        if (_model != null)
            return;

        _model = CreateModel();

        _model.BuffName = buffName;
        _model.BuffDescription = buffDescription;
        _model.IconPath = icon?.ResourcePath ?? "";
        _model.Duration = duration;
        _model.Superpositions = superpositions;
        _model.MaxSuperpositions = maxSuperpositions;
    }

    protected virtual BuffModel CreateModel() {
        return new BuffModel();
    }

    public void Update(double deltaTime, IUnitState unitState) {
        EnsureModelCreated();
        _model.Update(deltaTime, unitState);
    }

    public void AddSuperpositions(IBuff other) {
        EnsureModelCreated();
        _model.AddSuperpositions(other);
    }
}
