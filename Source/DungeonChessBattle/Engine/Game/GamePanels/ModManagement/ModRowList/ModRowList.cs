using System;
using System.Collections.Generic;
using DungeonChessBattle.Game.GamePanels.ModManagement.ModRowList;
using DungeonChessBattle.Game.Mod.Manager;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// mod 行列表组件：内部承载行卡片与空态占位，按行数自动切换二者显隐，
/// 外部只需调用 <see cref="Rebuild"/> 填入目录条目。
/// </summary>
public partial class ModRowList : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ModRowList> _logger = ServiceLocator.GetLogger<ModRowList>();

    /// <summary>导出引用集合节点。</summary>
    private ModRowListInterRefs? _refs;

    /// <summary>某一行发出的启停请求，转发给上层面板。</summary>
    public event Action<ModToggleRequest>? ToggleRequested;

    /// <summary>节点就绪：获取引用集合并同步一次初态。</summary>
    public override void _Ready() {
        _refs = GetNode<ModRowListInterRefs>("ModRowListInterRefs");
        if (_refs is null) {
            _logger.LogError("ModRowListInterRefs node not found.");
            return;
        }

        SyncEmptyState();
    }

    /// <summary>按目录条目重建全部行：空目录即清空后停在空态，非空逐条实例化卡片，末尾按行数同步空态。</summary>
    public void Rebuild(IReadOnlyList<ModEntryView>? mods) {
        Clear();
        if (mods is not null)
            foreach (ModEntryView mod in mods)
                AddRow(mod);

        SyncEmptyState();
    }

    private void AddRow(ModEntryView mod) {
        if (_refs is not { ItemScene: { } scene, ModRows: { } rows }) {
            _logger.LogError("ItemScene or ModRows is not ready, skip {ModId}.", mod.Id);
            return;
        }

        // 先入树再写数据：卡片的节点引用在 _Ready 才取到
        ModItem row = scene.Instantiate<ModItem>();
        row.ToggleRequested += OnToggleRequested;
        rows.AddChild(row);
        row.Setup(mod);
    }

    /// <summary>摘除旧行再排队回收；QueueFree 在本帧末才移除，先摘除使旧行立即退出容器。</summary>
    private void Clear() {
        if (_refs?.ModRows is not { } rows)
            return;

        foreach (Node stale in rows.GetChildren()) {
            rows.RemoveChild(stale);
            stale.QueueFree();
        }
    }

    private void OnToggleRequested(string modId, bool enabled) =>
        ToggleRequested?.Invoke(new ModToggleRequest(modId, enabled));

    /// <summary>空态即行数为零：空时显示 EmptyHint 并隐藏行容器，有行时相反。</summary>
    private void SyncEmptyState() {
        if (_refs?.ModRows is not { } rows)
            return;

        bool empty = rows.GetChildCount() == 0;
        _refs.EmptyHint?.Visible = empty;
        rows.Visible = !empty;
    }
}
