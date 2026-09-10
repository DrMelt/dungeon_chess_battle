using System.Linq;
using DungeonChessBattle.Game.Mod.Manager;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// mod 管理面板的单行卡片：整行按钮与名称、ID、构成、依赖、错误列，启停由独立 CheckBox 只读展示。
/// 列宽、配色与换行全由 mod_item.tscn 配置，本类只把一条 <see cref="ModPackage"/> 摊进六个控件。
/// 启停语义由 <see cref="ModManagementPanel"/> 裁决，卡片不接触目录与启用集。
/// </summary>
public partial class ModItem : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ModItem> _logger = ServiceLocator.GetLogger<ModItem>();

    /// <summary>整行按钮被点击时发出的信号，参数为本行 mod ID 与切换后的状态。</summary>
    [Signal]
    public delegate void ToggleRequestedEventHandler(string modId, bool enabled);

    /// <summary>导出引用集合节点。</summary>
    private ModItemInterRefs? _refs;

    /// <summary>本行对应的 mod ID，随信号上报。</summary>
    public string ModId { get; private set; } = "";

    /// <summary>节点就绪：获取引用集合并绑定整行按钮。</summary>
    public override void _Ready() {
        _refs = GetNode<ModItemInterRefs>("ModItemInterRefs");
        if (_refs is null) {
            _logger.LogError("ModItemInterRefs node not found.");
            return;
        }

        _refs.RowButton?.Pressed += OnRowPressed;
    }

    /// <summary>
    /// 写入一行 mod 数据，须在卡片进入场景树后调用——引用在 _Ready 才取到，入树前写入被丢弃。
    /// 无内容的列留空文本，显隐由场景决定。
    /// </summary>
    /// <param name="mod">管理视图条目。</param>
    public void Setup(ModPackage mod) {
        ModId = mod.Id;
        if (_refs is not { } refs)
            return;

        if (refs.EnableToggle is { } toggle) {
            // CheckBox 只读展示启停状态：走 no-signal 接口，不接收输入也不会上报
            toggle.SetPressedNoSignal(mod.IsEnabled);
            // 被拒载的目录启停无意义：它连内容都没读进来，勾选只会掩盖原因
            toggle.Disabled = mod.Reason is not null;
        }

        // manifest.name 不参与校验，缺席时用 ID 兜底：名字不该是一格空文本
        if (refs.ModNameLabel is { } nameLabel) {
            nameLabel.Text = string.IsNullOrEmpty(mod.Name) ? mod.Id : mod.Name;
            nameLabel.TooltipText = $"目录：{mod.DirectoryPath}\n版本：{mod.Version}";
        }

        refs.IdLabel?.Text = mod.Id;
        refs.CompositionLabel?.Text =
            $"代码 {(mod.HasCode ? "有" : "—")}\u3000展示 {(mod.HasDisplayCode ? "有" : "—")}\u3000优先级 {mod.Priority}";
        refs.DependencyLabel?.Text =
            mod.Dependencies.Count > 0 ? $"依赖 {string.Join("、", mod.Dependencies)}" : "";
        // 空集合 join 即空串，正合「没有错误」；只取原因，「modId: 原因」的整条形式留给面板底部汇总
        refs.ErrorLabel?.Text = string.Join("；", mod.Errors.Select(error => error.Message));
    }

    /// <summary>整行按钮被点击：请求切换本行启停状态，目标为当前展示状态的取反。</summary>
    private void OnRowPressed() {
        if (_refs?.EnableToggle is not { } toggle || toggle.Disabled)
            return;

        EmitSignal(SignalName.ToggleRequested, ModId, !toggle.ButtonPressed);
    }
}
