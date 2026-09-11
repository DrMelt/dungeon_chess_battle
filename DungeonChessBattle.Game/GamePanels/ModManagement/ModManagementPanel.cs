using System.Linq;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.Mod.Manager;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// mod 管理面板：列出 mods 目录下的 mod、切换启用集、呈现装载错误与内容修订号。
/// 本面板只读不判：mod 的解析、排序、启停落盘与错误汇总全在 <see cref="ModCatalog"/>。
/// 类内分两层——措辞是只进不出的纯函数，渲染只把文本与条目交给标签和 <see cref="ModRowList"/>；
/// 列宽、配色与换行都在场景里，这里不碰。
/// 启停改的是磁盘上的启用集，内容装配是一次性的，故变更需重启进程。
/// </summary>
public partial class ModManagementPanel : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ModManagementPanel> _logger = ServiceLocator.GetLogger<ModManagementPanel>();

    /// <summary>导出引用集合节点。</summary>
    private ModManagementPanelInterRefs? _refs;

    /// <summary>一次操作后附加在状态行之后的提示，null 表示无。</summary>
    private string? _notice;

    /// <summary>
    /// 节点就绪：绑定按钮。列表不在此构建——面板隐藏期目录可能已被用户改动，取数只发生在打开时。
    /// 某个按钮引用缺失只是那一个动作没有入口，不中断其余绑定，故逐条可空。
    /// </summary>
    public override void _Ready() {
        _refs = GetNode<ModManagementPanelInterRefs>("ModManagementPanelInterRefs");
        if (_refs is null) {
            _logger.LogError("ModManagementPanelInterRefs node not found.");
            return;
        }

        _refs.ModRowList?.ToggleRequested += OnToggleRequested;
        _refs.RescanButton?.Pressed += OnRescanPressed;
        _refs.OpenFolderButton?.Pressed += OnOpenFolderPressed;
        _refs.CloseButton?.Pressed += GoBack;
    }

    /// <summary>面板打开：重扫 mods 目录并刷新列表，反映用户在两次打开之间的改动。</summary>
    protected override void OnPanelOpened() {
        _notice = null;
        Refresh();
    }

    #region Rendering

    /// <summary>
    /// 按当前目录重建行列表与面板摘要。
    /// 行列表与摘要标签缺一即整体不渲染：半张面板会被读成「这里没有 mod」。
    /// </summary>
    private void Refresh() {
        if (_refs is not { ModRowList: { } rows, StatusLabel: { } status })
            return;

        ModAssets? assets = ServiceLocator.ModAssets;
        if (assets is null)
            _logger.LogWarning("ModAssets is null.");

        rows.Rebuild(assets?.Catalog.Packages);
        status.Text = SummaryFor(assets);
    }

    #endregion

    #region Text

    /// <summary>
    /// 面板摘要：状态主体在上，装载错误与最近操作提示依次在下，缺哪段就不出现哪段。
    /// 状态主体是启用集概况、mods 目录位置与运行中的数据修订号——房间与回放门控比的就是这个值。
    /// 磁盘启用集与装配那一刻的指纹不等时点出来，否则用户会撞上「改了开关却进不了自己的房」。
    /// 目录未装配时概况与修订号都无从谈起，只留目录位置。
    /// </summary>
    private string SummaryFor(ModAssets? assets) {
        string body = assets is null
            ? $"mod 内容未装配\nmods 目录：{ModManager.ModsRootPath}"
            : BuildStatusBody(assets);

        string errors = ErrorsFor(assets?.Catalog);
        if (errors.Length > 0)
            body = $"{body}\n\n{errors}";
        return WithNotice(body);
    }

    private static string BuildStatusBody(ModAssets assets) {
        ModCatalog catalog = assets.Catalog;
        string stale = catalog.Fingerprint == assets.AssemblyFingerprint
            ? ""
            : "\n磁盘启用集已变更，与运行中内容不一致，重启后才生效";
        return $"启用 {catalog.EnabledMods.Count} 个 · 停用 {catalog.DisabledCount} 个\n"
            + $"mods 目录：{ModManager.ModsRootPath}\n"
            + $"运行中数据修订号：{GameContentHost.Registry.DataRevision}"
            + stale;
    }

    /// <summary>
    /// 装载错误汇总：扫描、数据面装配、展示面装配三段合一，每条仍是「modId: 原因」全量。
    /// 卡片错误列只挑自己名下的那几条，这里补上不属于任何条目的部分；空串即无错。
    /// </summary>
    private static string ErrorsFor(ModCatalog? catalog) => catalog is null
        ? ""
        : string.Join('\n', catalog.Errors.Concat(catalog.AssemblyErrors).Concat(catalog.DisplayErrors));

    /// <summary>把最近一次操作的提示附在摘要末尾，无提示即原样。</summary>
    private string WithNotice(string text) => _notice is null ? text : $"{text}\n{_notice}";

    #endregion

    #region Button Handlers

    /// <summary>
    /// 启停一个 mod：只落盘启用集并刷新列表。已装配的内容与已注册的行为都不回滚，
    /// 故新状态要重启进程才生效——服务器子进程同样按重启后的启用集装配。
    /// </summary>
    private void OnToggleRequested(string modId, bool enabled) {
        string action = enabled ? "启用" : "停用";
        ModAssets? assets = ServiceLocator.ModAssets;
        if (assets is null)
            _notice = "mod 内容未装配，启停未落盘";
        else
            _notice = assets.Catalog.SetEnabled(modId, enabled)
                ? $"「{modId}」已{action}，重启游戏与服务器进程后生效"
                : $"启停未生效：{modId} 不在当前扫描结果内";
        Refresh();
    }

    /// <summary>重新扫描 mods 目录：发现新增或删除的 mod 目录。已装配的内容不变。</summary>
    private void OnRescanPressed() {
        ServiceLocator.ModAssets?.Catalog.Rescan();
        _notice = "已重新扫描目录；新增的 mod 需重启进程才会参与装配";
        Refresh();
    }

    /// <summary>打开 mods 目录：目录不存在则先建出来，省掉用户手找存档路径。成功即不留提示。</summary>
    private void OnOpenFolderPressed() {
        if (DirAccess.MakeDirRecursiveAbsolute(ModManager.ModsRootGodotPath) != Error.Ok)
            _logger.LogWarning("创建 mods 目录失败：{Path}", ModManager.ModsRootPath);

        if (OS.ShellOpen($"file://{ModManager.ModsRootPath}") != Error.Ok) {
            _logger.LogWarning("打开 mods 目录被系统拒绝");
            _notice = "系统未受理打开请求，请手动进入上方 mods 目录路径";
        }

        Refresh();
    }

    #endregion
}
