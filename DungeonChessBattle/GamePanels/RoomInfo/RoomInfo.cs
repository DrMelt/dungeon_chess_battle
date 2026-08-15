using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 房间信息卡片 UI 组件。显示副本名、房主、人数、状态与密码标记，支持点击选中。
/// 使用 InterRefs 模式分离 [Export] 引用。
/// </summary>
public partial class RoomInfo : Container {
    /// <summary>房间被点击选中时发出的信号，参数为房间 ID。</summary>
    [Signal]
    public delegate void RoomSelectedEventHandler(string roomId);

    /// <summary>日志记录器。</summary>
    private static readonly ILogger<RoomInfo> _logger = ServiceLocator.GetLogger<RoomInfo>();

    /// <summary>导出引用集合节点。</summary>
    private RoomInfoInterRefs? _refs;

    /// <summary>是否处于选中状态。</summary>
    private bool _isSelected;

    /// <summary>未选中时的原始背景色。</summary>
    private Color _normalBgColor;
    /// <summary>选中状态下的背景色。</summary>
    private static readonly Color SelectedBgColor = new(0.3f, 0.6f, 1.0f, 0.7f);

    /// <summary>当前房间 ID。</summary>
    public string RoomId { get; private set; } = "";

    /// <summary>暂存的副本名文本（_Ready 前写入，进入场景树后应用）。</summary>
    private string _dungeonText = "";
    /// <summary>暂存的密码标记文本。</summary>
    private string _passwordText = "";
    /// <summary>暂存的房间状态文本。</summary>
    private string _statusText = "";
    /// <summary>暂存的房主文本。</summary>
    private string _hostText = "";
    /// <summary>暂存的人数文本。</summary>
    private string _playersText = "";
    /// <summary>暂存的副本介绍文本。</summary>
    private string _descriptionText = "";

    /// <summary>
    /// 节点就绪：获取引用集合、缓存正常背景色、应用暂存文本并连接鼠标交互事件。
    /// </summary>
    public override void _Ready() {
        _refs = GetNode<RoomInfoInterRefs>("RoomInfoInterRefs");
        if (_refs is null) {
            _logger.LogError("RoomInfoInterRefs node not found.");
            return;
        }

        if (_refs.BgPanel?.GetThemeStylebox("panel") is StyleBoxFlat flat) {
            _normalBgColor = flat.BgColor;
        }

        ApplyTexts();

        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// 设置房间数据显示。
    /// </summary>
    /// <param name="room">房间列表条目。</param>
    public void Setup(RoomListing room) {
        UpdateListing(room);
    }

    /// <summary>
    /// 全量刷新房间卡片显示。
    /// </summary>
    /// <param name="room">房间列表条目。</param>
    public void UpdateListing(RoomListing room) {
        RoomId = room.RoomId;
        _dungeonText = string.IsNullOrEmpty(room.DungeonName) ? room.DungeonKey : room.DungeonName;
        _passwordText = room.HasPassword ? "🔒" : "";
        _statusText = GetStatusText(room.Status);
        _hostText = $"房主: {room.HostName}";
        _playersText = $"人数: {room.CurrentPlayers}/{room.MaxPlayers}";
        _descriptionText = room.Description;
        ApplyTexts();
    }

    /// <summary>
    /// 将暂存文本应用到实际标签（引用未就绪时静默，_Ready 后再次调用）。
    /// </summary>
    private void ApplyTexts() {
        if (_refs is null)
            return;
        _refs.DungeonLabel?.SetText(_dungeonText);
        _refs.PasswordLabel?.SetText(_passwordText);
        _refs.StatusLabel?.SetText(_statusText);
        _refs.HostLabel?.SetText(_hostText);
        _refs.PlayersLabel?.SetText(_playersText);
        _refs.DescriptionLabel?.SetText(_descriptionText);
    }

    /// <summary>
    /// 生成房间状态文字。
    /// </summary>
    /// <param name="status">房间状态。</param>
    private static string GetStatusText(RoomStatus status) =>
        status != RoomStatus.Finished ? "等待中" : "已结束";

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
