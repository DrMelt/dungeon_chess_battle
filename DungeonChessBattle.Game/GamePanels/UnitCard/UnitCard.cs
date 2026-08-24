using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 单位卡片组件。在房间准备界面中展示可选单位，支持点击选择。
/// 替代旧版 UnitSelectCard，统一为蓝色主题（Camp A）。
/// 使用 InterRefs 模式分离 [Export] 引用。
/// </summary>
public partial class UnitCard : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitCard> _logger = ServiceLocator.GetLogger<UnitCard>();

    /// <summary>单位被点击选中时发出的信号，参数为单位配置键。</summary>
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitConfigKey);

    /// <summary>导出引用集合节点。</summary>
    private UnitCardInterRefs? _refs;

    /// <summary>是否处于选中状态。</summary>
    private bool _isSelected;

    /// <summary>普通状态背景调制色（_Ready 缓存默认白色，供悬停/选中后恢复）。</summary>
    private Color _normalTint;
    /// <summary>悬停状态背景调制色：分量超过 1 以提亮底色。</summary>
    private static readonly Color HoverTint = new(1.15f, 1.15f, 1.15f, 1f);
    /// <summary>选中状态背景调制色：蓝色调。</summary>
    private static readonly Color SelectedTint = new(0.55f, 0.85f, 1.0f, 1f);

    /// <summary>当前单位配置键。</summary>
    public string UnitConfigKey { get; private set; } = "";

    /// <summary>暂存的职业名（_Ready 前写入，进入场景树后应用）。</summary>
    private string _nameText = "";
    /// <summary>暂存的 HP 数值文本（由 SetupUnit 格式化，仅承载 HP 数值）。</summary>
    private string _hpValueText = "";
    /// <summary>暂存的玩家名。</summary>
    private string _userName = "";

    /// <summary>
    /// 节点就绪：获取引用集合、缓存正常背景色、应用暂存文本并连接鼠标交互事件。
    /// </summary>
    public override void _Ready() {
        _refs = GetNode<UnitCardInterRefs>("UnitCardInterRefs");
        if (_refs is null) {
            _logger.LogError("UnitCardInterRefs node not found.");
            return;
        }

        _normalTint = _refs.BgPanel?.SelfModulate ?? Colors.White;

        ApplyTexts();

        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// 设置卡片显示的单位信息。可在节点进入场景树前调用，进入后自动生效。
    /// 职业名直接使用配置键。HpValueLabel 仅承载 HP 数值，由 HP_Label 提供 "HP: " 前缀。
    /// </summary>
    /// <param name="configKey">单位配置键，亦是显示名。</param>
    /// <param name="maxHealth">最大生命值。</param>
    public void SetupUnit(string configKey, float maxHealth) {
        UnitConfigKey = configKey;
        _nameText = configKey;
        _hpValueText = maxHealth.ToString("F0");
        ApplyTexts();
    }

    /// <summary>
    /// 设置用户名标签。可在节点进入场景树前调用。
    /// </summary>
    public void SetUserName(string userName) {
        _userName = userName;
        ApplyTexts();
    }

    /// <summary>
    /// 以未选择职业的占位状态显示卡片：仅展示所属玩家名。
    /// </summary>
    /// <param name="playerName">玩家名。</param>
    public void SetPlaceholder(string playerName) {
        UnitConfigKey = "";
        _nameText = "未选择";
        _hpValueText = "—";
        _userName = playerName;
        ApplyTexts();
    }

    /// <summary>
    /// 将暂存文本应用到实际标签（引用未就绪时静默，_Ready 后再次调用）。
    /// </summary>
    private void ApplyTexts() {
        if (_refs is null)
            return;
        _refs.NameLabel?.SetText(_nameText);
        _refs.HpValueLabel?.SetText(_hpValueText);
        _refs.UserNameLabel?.SetText(_userName);
    }

    /// <summary>
    /// 设置选中高亮状态。
    /// </summary>
    public void SetSelected(bool selected) {
        _isSelected = selected;
        UpdateVisualState();
    }

    /// <summary>
    /// 根据选中状态刷新背景调制色：选中为蓝色调，否则恢复普通色。
    /// </summary>
    private void UpdateVisualState() {
        if (_refs?.BgPanel is null)
            return;

        _refs.BgPanel.SelfModulate = _isSelected ? SelectedTint : _normalTint;
    }

    /// <summary>
    /// 处理鼠标左键点击，发出选中信号。
    /// </summary>
    /// <param name="event">输入事件。</param>
    private void OnGuiInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed) {
            EmitSignal(SignalName.UnitSelected, UnitConfigKey);
            AcceptEvent();
        }
    }

    /// <summary>
    /// 鼠标移入时经调制提亮背景色（未选中状态下）。
    /// </summary>
    private void OnMouseEntered() {
        if (_isSelected)
            return;

        if (_refs?.BgPanel is not null)
            _refs.BgPanel.SelfModulate = HoverTint;
    }

    /// <summary>
    /// 鼠标移出时恢复原始背景色。
    /// </summary>
    private void OnMouseExited() {
        if (!_isSelected) {
            UpdateVisualState();
        }
    }
}
