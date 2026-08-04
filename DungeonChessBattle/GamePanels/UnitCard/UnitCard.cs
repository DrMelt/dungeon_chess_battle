using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 单位卡片组件。在房间准备界面中展示可选单位，支持点击选择。
/// 替代旧版 UnitSelectCard，统一为蓝色主题（Camp A）。
/// 使用 InterRefs 模式分离 [Export] 引用。
/// </summary>
public partial class UnitCard : Control {
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitConfigKey);

    private UnitCardInterRefs? _refs;

    private string _unitConfigKey = "";
    private bool _isSelected;

    private Color _normalBgColor;
    private static readonly Color SelectedBgColor = new(0.3f, 0.6f, 1.0f, 0.7f);

    public string UnitConfigKey => _unitConfigKey;

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
        _unitConfigKey = configKey;
        _refs?.NameLabel?.Text = displayName;
        _refs?.HpValueLabel?.Text = statsText;
    }

    /// <summary>
    /// 设置用户名标签。
    /// </summary>
    public void SetUserName(string userName) {
        _refs?.UserNameLabel?.Text = userName;
    }

    public void SetSelected(bool selected) {
        _isSelected = selected;
        UpdateVisualState();
    }

    private void UpdateVisualState() {
        if (_refs?.BgPanel?.GetThemeStylebox("panel") is not StyleBoxFlat flat)
            return;

        if (_isSelected)
            flat.BgColor = SelectedBgColor;
        else
            flat.BgColor = _normalBgColor;
    }

    private void OnGuiInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed) {
            EmitSignal(SignalName.UnitSelected, _unitConfigKey);
            AcceptEvent();
        }
    }

    private void OnMouseEntered() {
        if (_isSelected)
            return;

        if (_refs?.BgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            flat.BgColor = _normalBgColor.Lightened(0.15f);
        }
    }

    private void OnMouseExited() {
        if (!_isSelected) {
            UpdateVisualState();
        }
    }
}
