using System;
using System.Collections.Generic;
using DungeonChessBattle.Game.Common;
using DungeonChessBattle.Battle.Shared.Combat;
using Godot;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// Buff 图标容器，同步单位展示视图的 Buff 列表到图标视图。
/// 数据源为 <see cref="IUnitUiView.Buffs"/>（在线来自本地镜像，回放来自 BattleUnit），复用 CacheSynchronizer：
/// 键为 BuffTypeId，仅在列表增删时建/删图标，内容变化由 update 回调刷新对应图标，
/// 剩余时间由图标每帧按本地 Buff 剩余时间刷新。
/// </summary>
public partial class ContainerBuffs : Control {
    /// <summary>导出引用集合节点。</summary>
    public ContainerBuffsInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>Buff 图标缓存，键为 BuffTypeId，源列表因 AddBuff 按类型合并而唯一。</summary>
    private readonly CacheSynchronizer<ushort, IBuffUiView, TextureRectBuffIcon> _icons;

    /// <summary>当前绑定单位展示视图，update 回调用于来源着色与剩余时间推算。</summary>
    private IUnitUiView? _focusUnit;

    /// <summary>构造函数：注入键提取、创建、移除与更新回调。</summary>
    public ContainerBuffs() {
        _icons = new(GetKey, CreateIcon, RemoveIcon, UpdateIcon);
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<ContainerBuffsInterRefs>("ContainerBuffsInterRefs");
    }

    /// <summary>
    /// 按当前单位刷新 Buff 图标：同步源列表，仅增删时建/删图标，内容变化由图标内部短路刷新。
    /// </summary>
    /// <param name="unit">目标单位展示视图，null 表示清空。</param>
    public void UpdateUI_WithUnit(IUnitUiView? unit) {
        _focusUnit = unit;
        if (InterRefs == null)
            return;
        IReadOnlyList<IBuffUiView> source = unit?.Buffs ?? [];
        _icons.Sync(source);
    }

    /// <summary>提取 Buff 类型 ID 作为图标键。</summary>
    private static ushort GetKey(IBuffUiView buff) => buff.BuffTypeId;

    /// <summary>创建 Buff 图标并挂载到容器。</summary>
    private TextureRectBuffIcon CreateIcon() {
        if (InterRefs?.BuffIconPackedScene is not { } scene
            || InterRefs.BuffContainer is not { } container)
            throw new InvalidOperationException("[ContainerBuffs] BuffIconPackedScene or BuffContainer not assigned.");
        var icon = scene.Instantiate<TextureRectBuffIcon>();
        container.AddChild(icon);
        return icon;
    }

    /// <summary>移除 Buff 图标。</summary>
    private static void RemoveIcon(TextureRectBuffIcon icon) => icon.QueueFree();

    /// <summary>刷新图标内容；同单位且数据未变化时由图标内部短路。</summary>
    private void UpdateIcon(TextureRectBuffIcon icon, IBuffUiView buff) {
        if (_focusUnit is { } unit)
            icon.SetBuffIcon(buff, unit);
    }
}
