using System.Linq;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.Mod;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// mod 管理面板：列出 mods 目录下的 mod、切换启用集、呈现装载错误与内容修订号。
/// 本面板只读不判：mod 的解析、排序、启停落盘与错误汇总全在 <see cref="ModCatalog"/>，
/// 这里只做一行一控件的呈现。启停改的是磁盘上的启用集，内容装配是一次性的，故变更需重启进程。
/// </summary>
public partial class ModManagementPanel : BaseGamePanel {
    /// <summary>mod 名列宽度。</summary>
    private const float NameWidth = 240f;
    /// <summary>mod ID 列宽度。</summary>
    private const float IdWidth = 170f;
    /// <summary>构成列宽度。</summary>
    private const float CompositionWidth = 190f;

    private static readonly Color DimColor = new(0.62f, 0.62f, 0.62f, 1f);
    private static readonly Color ErrorColor = new(0.9f, 0.42f, 0.36f, 1f);

    private readonly ILogger<ModManagementPanel> _logger = ServiceLocator.GetLogger<ModManagementPanel>();

    /// <summary>导出引用集合节点。</summary>
    public ModManagementPanelInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>一次操作后附加在状态行之后的提示，null 表示无。</summary>
    private string? _notice;

    /// <summary>
    /// 节点就绪：绑定按钮。列表不在此构建——面板隐藏期目录可能已被用户改动，取数只发生在打开时。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<ModManagementPanelInterRefs>("ModManagementPanelInterRefs");
        if (InterRefs is null) {
            _logger.LogError("ModManagementPanelInterRefs node not found.");
            return;
        }

        InterRefs?.RescanButton?.Pressed += OnRescanPressed;
        InterRefs?.OpenFolderButton?.Pressed += OnOpenFolderPressed;
        InterRefs?.CloseButton?.Pressed += GoBack;
    }

    /// <summary>面板打开：重扫 mods 目录并刷新列表，反映用户在两次打开之间的改动。</summary>
    protected override void OnPanelOpened() {
        _notice = null;
        Refresh();
    }

    #region Rendering

    /// <summary>重建列表、刷新状态与错误。</summary>
    private void Refresh() {
        if (InterRefs?.ModList is not { } list || InterRefs.StatusLabel is not { } statusLabel)
            return;

        foreach (Node stale in list.GetChildren().ToArray()) {
            list.RemoveChild(stale);
            stale.QueueFree();
        }
        ModCatalog? catalog = ModAssets.Catalog;
        if (catalog is null) {
            list.AddChild(SingleLine("mod 尚未装配。"));
            return;
        }

        foreach (ModPackage mod in catalog.Packages)
            list.AddChild(CreateRow(mod));
        if (catalog.Packages.Count == 0)
            list.AddChild(SingleLine("未发现任何 mod。把 mod 包放进下方 mods 目录，每个 mod 一个子目录。"));

        statusLabel.Text = BuildStatus(catalog);

        var errors = catalog.Errors
            .Concat(catalog.AssemblyErrors)
            .Concat(catalog.DisplayErrors)
            .ToList();
        if (InterRefs?.ErrorLabel is { } errorLabel) {
            errorLabel.Text = errors.Count > 0 ? string.Join("\n", errors) : "";
            errorLabel.Visible = errors.Count > 0;
        }
    }

    /// <summary>构造单个 mod 的一行：启用开关、ID、构成、问题说明。</summary>
    private HBoxContainer CreateRow(ModPackage mod) {
        var row = new HBoxContainer();

        var toggle = new CheckBox {
            ButtonPressed = mod.IsEnabled,
            Text = string.IsNullOrEmpty(mod.Name) ? mod.Id : mod.Name,
            CustomMinimumSize = new Vector2(NameWidth, 0),
            TooltipText = $"目录名：{mod.Id}\n版本：{mod.Version}\n优先级：{mod.Priority}",
            // 被拒载的目录启停无意义：它连内容都没读进来，勾选只会掩盖原因
            Disabled = mod.Reason is not null,
        };
        string captured = mod.Id;
        toggle.Toggled += on => OnToggleRequested(captured, on);
        row.AddChild(toggle);

        // ID 列恒为次要色：停用是用户意图不是故障，故障只由问题列以红色表达
        row.AddChild(Cell(mod.Id, IdWidth, DimColor));
        row.AddChild(Cell(CompositionOf(mod), CompositionWidth, DimColor));
        row.AddChild(Cell(ProblemOf(mod), 0, mod.Errors.Count > 0 ? ErrorColor : DimColor, expand: true));
        return row;
    }

    /// <summary>单元格标签；宽度为 0 表示按剩余空间伸展。</summary>
    private static Label Cell(string text, float width, Color color, bool expand = false) {
        var label = new Label { Text = text, Modulate = color };
        if (width > 0)
            label.CustomMinimumSize = new Vector2(width, 0);
        if (expand) {
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.AutowrapMode = TextServer.AutowrapMode.Word;
        }
        return label;
    }

    private static Label SingleLine(string text) => Cell(text, 0, DimColor, expand: true);

    /// <summary>构成列：数据代码与展示代码是否齐备。</summary>
    private static string CompositionOf(ModPackage mod) =>
        $"代码 {(mod.HasCode ? "有" : "—")}　展示 {(mod.HasDisplayCode ? "有" : "—")}　优先级 {mod.Priority}";

    /// <summary>问题列：优先显示该 mod 的装载错误，其次显示其依赖关系。</summary>
    private static string ProblemOf(ModPackage mod) {
        if (mod.Errors.Count > 0)
            return string.Join("；", mod.Errors);
        return mod.Dependencies.Count > 0 ? $"依赖 {string.Join("、", mod.Dependencies)}" : "";
    }

    /// <summary>
    /// 状态行：启用集概况、mods 目录位置与运行中的数据修订号——房间与回放门控比的就是这个值。
    /// 磁盘启用集与装配那一刻的指纹不等时点出来，否则用户会撞上「改了开关却进不了自己的房」。
    /// </summary>
    private string BuildStatus(ModCatalog catalog) {
        string stale = catalog.Fingerprint == ModAssets.AssemblyFingerprint
            ? ""
            : "\n磁盘启用集已变更，与运行中内容不一致，重启后才生效";
        string facts = $"启用 {catalog.EnabledMods.Count} 个 · 停用 {catalog.DisabledCount} 个"
            + $"\nmods 目录：{ModManager.ModsRootPath}"
            + $"\n运行中数据修订号：{GameContentHost.Registry.DataRevision}"
            + stale;
        return _notice is null ? facts : $"{facts}\n{_notice}";
    }

    #endregion

    #region Button Handlers

    /// <summary>
    /// 启停一个 mod：只落盘启用集并刷新列表。已装配的内容与已注册的行为都不回滚，
    /// 故新状态要重启进程才生效——服务器子进程同样按重启后的启用集装配。
    /// </summary>
    private void OnToggleRequested(string modId, bool enabled) {
        _notice = ModAssets.SetEnabled(modId, enabled)
            ? $"「{modId}」已{(enabled ? "启用" : "停用")}，重启游戏与服务器进程后生效"
            : $"启停未生效：{modId} 不在当前扫描结果内";
        Refresh();
    }

    /// <summary>重新扫描 mods 目录：发现新增或删除的 mod 目录。已装配的内容不变。</summary>
    private void OnRescanPressed() {
        ModAssets.Catalog?.Rescan();
        _notice = "已重新扫描目录；新增的 mod 需重启进程才会参与装配";
        Refresh();
    }

    /// <summary>打开 mods 目录：目录不存在则先建出来，省掉用户手找存档路径。</summary>
    private void OnOpenFolderPressed() {
        if (DirAccess.MakeDirRecursiveAbsolute("user://mods") != Error.Ok)
            _logger.LogWarning("创建 mods 目录失败：{Path}", ModManager.ModsRootPath);

        if (OS.ShellOpen($"file://{ModManager.ModsRootPath}") != Error.Ok) {
            _logger.LogWarning("打开 mods 目录被系统拒绝");
            _notice = "系统未受理打开请求，请手动进入上方 mods 目录路径";
        }
        Refresh();
    }

    #endregion
}
