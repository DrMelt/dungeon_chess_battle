using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class BuffBaseGodot : Resource, IBuff {
    protected BuffModel? _model = null;

    /// <summary>
    /// 指向 GameConfigDB 的配置 ID，数值全部从 C# 配置读取
    /// </summary>
    [Export]
    public string buffConfigId = "";

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

        var config = !string.IsNullOrEmpty(buffConfigId) ? GameConfigDB.GetBuff(buffConfigId) : null;
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
