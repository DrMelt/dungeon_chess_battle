using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 房间信息卡片 UI 组件。显示房间 ID、状态，支持点击选中。
/// </summary>
public partial class RoomInfo : Container {
    /// <summary>房间被点击选中时发出的信号，参数为房间 ID。</summary>
    [Signal]
    public delegate void RoomSelectedEventHandler(string roomId);

    /// <summary>是否处于选中状态。</summary>
    private bool _isSelected;

    /// <summary>房间信息标签。</summary>
    [Export] private Label? _label;
    /// <summary>背景面板，用于高亮效果。</summary>
    [Export] private Panel? _bgPanel;

    // 缓存原始背景色用于高亮切换
    /// <summary>未选中时的原始背景色。</summary>
    private Color _normalBgColor;
    /// <summary>选中状态下的背景色。</summary>
    private static readonly Color SelectedBgColor = new(0.3f, 0.6f, 1.0f, 0.7f);

    /// <summary>当前房间 ID。</summary>
    public string RoomId { get; private set; } = "";

    /// <summary>
    /// 节点就绪：缓存正常背景色并连接鼠标交互事件。
    /// </summary>
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
    /// <param name="roomId">房间 ID。</param>
    /// <param name="statusText">房间状态文本。</param>
    public void Setup(string roomId, string statusText) {
        RoomId = roomId;
        _label?.Text = $"{roomId}  [{statusText}]";
    }

    /// <summary>
    /// 更新房间状态文本。
    /// </summary>
    /// <param name="statusText">房间状态文本。</param>
    public void UpdateStatus(string statusText) {
        _label?.Text = $"{RoomId}  [{statusText}]";
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
        if (_bgPanel?.GetThemeStylebox("panel") is not StyleBoxFlat flat)
            return;

        if (_isSelected) {
            flat.BgColor = SelectedBgColor;
        }
        else {
            flat.BgColor = _normalBgColor;
        }
    }

    /// <summary>
    /// 处理鼠标左键点击，发出选中信号。
    /// </summary>
    /// <param name="event">输入事件。</param>
    private void OnGuiInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed) {
            EmitSignal(SignalName.RoomSelected, RoomId);
            AcceptEvent();
        }
    }

    /// <summary>
    /// 鼠标移入时轻微提亮背景色（未选中状态下）。
    /// </summary>
    private void OnMouseEntered() {
        if (_isSelected)
            return;

        if (_bgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
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
