using System;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// Godot Buff 基类资源，桥接 BuffConfig 配置与运行时 BuffModel 逻辑。
/// </summary>
[GlobalClass]
public partial class BuffBaseGodot : Resource, IBuff {
    /// <summary>运行时 Buff 数据模型，懒加载创建。</summary>
    protected BuffModel? _model = null;

    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的 BuffConfig（类型安全，编译期检查）
    /// </summary>
    protected virtual BuffConfig? Config => null;

    /// <summary>Buff 图标。</summary>
    [Export]
    public Texture2D? icon;

    /// <summary>Buff 名称。</summary>
    [field: Export]
    public string BuffName { get; private set; } = "";
    /// <summary>Buff 描述。</summary>
    [field: Export]
    public string BuffDescription { get; private set; } = "";
    /// <summary>图标资源路径。</summary>
    public string IconPath => icon?.ResourcePath ?? "";
    /// <summary>剩余持续时间。</summary>
    public double Duration => _model?.Duration ?? 0;
    /// <summary>当前层数。</summary>
    public int Superpositions => _model?.Superpositions ?? 1;
    /// <summary>最大层数。</summary>
    public int MaxSuperpositions => _model?.MaxSuperpositions ?? 1;
    /// <summary>Buff 是否仍然生效。</summary>
    public bool IsAlive => _model?.IsAlive ?? throw new InvalidOperationException("Buff model has not been initialized.");
    /// <summary>施加该 Buff 的来源单位。</summary>
    public IUnitState? FromUnit => _model?.FromUnit ?? throw new InvalidOperationException("Buff model has not been initialized.");

    /// <summary>
    /// 确保运行时模型已创建；未创建时依据 Config 懒加载生成。
    /// </summary>
    private void EnsureModelCreated() {
        if (_model != null)
            return;

        var config = Config ?? throw new InvalidOperationException(
                $"Buff '{GetType().Name}' must override the Config property to provide a valid BuffConfig. " +
                "Config returned null, which means this buff has no configuration.");

        _model = GameConfigDB.ToBuffModel(config);
        _model.BuffName = BuffName;
    }

    /// <summary>
    /// 按帧更新 Buff 计时与效果。
    /// </summary>
    /// <param name="deltaTime">距上一帧的秒数。</param>
    /// <param name="unitState">目标单位状态。</param>
    public void Update(double deltaTime, IUnitState unitState) {
        EnsureModelCreated();
        _model?.Update(deltaTime, unitState);
    }

    /// <summary>
    /// 叠加 Buff 层数。
    /// </summary>
    /// <param name="other">另一份同类型 Buff。</param>
    public void AddSuperpositions(IBuff other) {
        EnsureModelCreated();
        _model?.AddSuperpositions(other);
    }
}
