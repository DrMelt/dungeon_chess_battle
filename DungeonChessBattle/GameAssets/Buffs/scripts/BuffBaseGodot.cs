using System;
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
    public Texture2D? icon;

    [Export]
    string _buffName = "";
    [Export]
    string _buffDescription = "";

    public string BuffName => _buffName;
    public string BuffDescription => _buffDescription;
    public string IconPath => icon?.ResourcePath ?? "";
    public double Duration => _model?.Duration ?? 0;
    public int Superpositions => _model?.Superpositions ?? 1;
    public int MaxSuperpositions => _model?.MaxSuperpositions ?? 1;
    public bool IsAlive => _model?.IsAlive ?? throw new InvalidOperationException("Buff model has not been initialized.");
    public IUnitState? FromUnit => _model?.FromUnit ?? throw new InvalidOperationException("Buff model has not been initialized.");

    private void EnsureModelCreated() {
        if (_model != null)
            return;

        var config = Config ?? throw new InvalidOperationException(
                $"Buff '{GetType().Name}' must override the Config property to provide a valid BuffConfig. " +
                "Config returned null, which means this buff has no configuration.");

        _model = GameConfigDB.ToBuffModel(config);
        _model.BuffName = _buffName;
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
