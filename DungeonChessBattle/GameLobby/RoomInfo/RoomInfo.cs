using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 房间信息卡片 UI 组件。显示房间 ID、状态，支持点击选中。
/// </summary>
public partial class RoomInfo : Container {
    [Signal]
    public delegate void RoomSelectedEventHandler(string roomId);

    private string _roomId = "";
    private bool _isSelected;

    [Export] private Label? _label;
    [Export] private Panel? _bgPanel;

    // 缓存原始背景色用于高亮切换
    private Color _normalBgColor;
    private static readonly Color SelectedBgColor = new(0.3f, 0.6f, 1.0f, 0.7f);

    public string RoomId => _roomId;

    public override void _Ready() {
        // 缓存正常背景色
        if (_bgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            _normalBgColor = flat.BgColor;
        }

        // 连接输入事件用于点击
        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// 设置房间数据显示。
    /// </summary>
    public void Setup(string roomId, string statusText) {
        _roomId = roomId;
        _label?.Text = $"{roomId}  [{statusText}]";
    }

    /// <summary>
    /// 更新房间状态文本。
    /// </summary>
    public void UpdateStatus(string statusText) {
        _label?.Text = $"{_roomId}  [{statusText}]";
    }

    /// <summary>
    /// 设置选中高亮状态。
    /// </summary>
    public void SetSelected(bool selected) {
        _isSelected = selected;
        UpdateVisualState();
    }

    private void UpdateVisualState() {
        if (_bgPanel?.GetThemeStylebox("panel") is not StyleBoxFlat flat)
            return;

        if (_isSelected) {
            flat.BgColor = SelectedBgColor;
        }
        else {
            flat.BgColor = _normalBgColor;
        }
    }

    private void OnGuiInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed) {
            EmitSignal(SignalName.RoomSelected, _roomId);
            AcceptEvent();
        }
    }

    private void OnMouseEntered() {
        if (_isSelected)
            return;

        if (_bgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            flat.BgColor = _normalBgColor.Lightened(0.15f);
        }
    }

    private void OnMouseExited() {
        if (!_isSelected) {
            UpdateVisualState();
        }
    }
}
