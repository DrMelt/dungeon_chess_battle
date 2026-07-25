using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class BuffBaseGodot : Resource, IBuff {
    protected BuffModel? _model = null;

    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的 BuffConfig（类型安全，编译期检查）
    /// </summary>
    protected virtual BuffConfig? Config => null;

    [Export]
    public Texture2D icon = null!;

    public string BuffName => _model?.BuffName ?? "";
    public string BuffDescription => _model?.BuffDescription ?? "";
    public string IconPath => _model?.IconPath ?? icon?.ResourcePath ?? "";
    public double Duration => _model?.Duration ?? 0;
    public int Superpositions => _model?.Superpositions ?? 1;
    public int MaxSuperpositions => _model?.MaxSuperpositions ?? 1;
    public bool IsAlive => _model?.IsAlive ?? true;
    public IUnitState FromUnit => _model?.FromUnit!;

    private void EnsureModelCreated() {
        if (_model != null)
            return;

        var config = Config;
        _model = config != null ? GameConfigDB.ToBuffModel(config) : new BuffModel();
        _model.IconPath = icon?.ResourcePath ?? "";
    }

    public void Update(double deltaTime, IUnitState unitState) {
        EnsureModelCreated();
        _model?.Update(deltaTime, unitState);
    }

    public void AddSuperpositions(IBuff other) {
        EnsureModelCreated();
        _model?.AddSuperpositions(other);
    }
}
