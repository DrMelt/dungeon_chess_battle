using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 单位卡片组件。在房间准备界面中展示可选单位，支持点击选择。
/// 替代旧版 UnitSelectCard，统一为蓝色主题（Camp A）。
/// 使用 InterRefs 模式分离 [Export] 引用。
/// </summary>
public partial class UnitCard : Control {
    /// <summary>单位被点击选中时发出的信号，参数为单位配置键。</summary>
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitConfigKey);

    /// <summary>导出引用集合节点。</summary>
    private UnitCardInterRefs? _refs;

    /// <summary>是否处于选中状态。</summary>
    private bool _isSelected;

    /// <summary>未选中时的原始背景色。</summary>
    private Color _normalBgColor;
    /// <summary>选中状态下的背景色。</summary>
    private static readonly Color SelectedBgColor = new(0.3f, 0.6f, 1.0f, 0.7f);

    /// <summary>当前单位配置键。</summary>
    public string UnitConfigKey { get; private set; } = "";

    /// <summary>
    /// 节点就绪：获取引用集合、缓存正常背景色并连接鼠标交互事件。
    /// </summary>
    public override void _Ready() {
        _refs = GetNode<UnitCardInterRefs>("UnitCardInterRefs");
        if (_refs is null) {
            GD.PrintErr("[UnitCard] UnitCardInterRefs node not found.");
            return;
        }

        if (_refs.BgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            _normalBgColor = flat.BgColor;
        }

        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// 设置卡片显示的单位信息。
    /// statsText 写入 HpValueLabel。
    /// </summary>
    public void Setup(string configKey, string displayName, string statsText) {
        UnitConfigKey = configKey;
        _refs?.NameLabel?.Text = displayName;
        _refs?.HpValueLabel?.Text = statsText;
    }

    /// <summary>
    /// 设置用户名标签。
    /// </summary>
    public void SetUserName(string userName) {
        _refs?.UserNameLabel?.Text = userName;
    }

    /// <summary>
    /// 设置选中高亮状态。
    /// </summary>
    public void SetSelected(bool selected) {
        _isSelected = selected;
        UpdateVisualState();
    }

    /// <summary>
    /// 根据选中状态刷新背景高亮。
    /// </summary>
    private void UpdateVisualState() {
        if (_refs?.BgPanel?.GetThemeStylebox("panel") is not StyleBoxFlat flat)
            return;

        if (_isSelected)
            flat.BgColor = SelectedBgColor;
        else
            flat.BgColor = _normalBgColor;
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
    /// 鼠标移入时轻微提亮背景色（未选中状态下）。
    /// </summary>
    private void OnMouseEntered() {
        if (_isSelected)
            return;

        if (_refs?.BgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            flat.BgColor = _normalBgColor.Lightened(0.15f);
        }
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
