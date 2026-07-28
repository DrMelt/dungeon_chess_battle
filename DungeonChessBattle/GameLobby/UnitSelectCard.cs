using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 单位选择卡片组件。在房间准备界面中展示可选单位，支持点击选择。
/// </summary>
public partial class UnitSelectCard : Container {
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitConfigKey);

    private string _unitConfigKey = "";
    private string _unitDisplayName = "";
    private bool _isSelected;

    [Export] private Label _nameLabel = null!;
    [Export] private Label _statsLabel = null!;
    [Export] private Panel _bgPanel = null!;

    private Color _normalBgColor;
    private static readonly Color SelectedBgColor = new(0.3f, 0.6f, 1.0f, 0.7f);

    public string UnitConfigKey => _unitConfigKey;

    public override void _Ready() {
        if (_bgPanel.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            _normalBgColor = flat.BgColor;
        }

        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// 设置卡片显示的单位信息。
    /// </summary>
    public void Setup(string configKey, string displayName, string statsText) {
        _unitConfigKey = configKey;
        _unitDisplayName = displayName;
        _nameLabel.Text = displayName;
        _statsLabel.Text = statsText;
    }

    public void SetSelected(bool selected) {
        _isSelected = selected;
        UpdateVisualState();
    }

    private void UpdateVisualState() {
        if (_bgPanel.GetThemeStylebox("panel") is not StyleBoxFlat flat)
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

        if (_bgPanel.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            flat.BgColor = _normalBgColor.Lightened(0.15f);
        }
    }

    private void OnMouseExited() {
        if (!_isSelected) {
            UpdateVisualState();
        }
    }
}