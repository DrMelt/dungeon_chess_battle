using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Lobby.Protocol.Dtos;
using DungeonChessBattle.Client;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.GameConfig.Models;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 房间准备界面。玩家进入房间后选择阵营单位并准备，房主在全员准备后开始战斗。
/// 房主显示"开始战斗"（等待其他玩家全部准备），非房主显示"准备"/"取消准备"切换。
/// 准备阶段经客户端门面发大厅请求：单位增删、准备切换与战斗启动。
/// 战斗启动后服务端返回端口重定向，连接由门面切到房间 LES 链路。
/// </summary>
public partial class RoomPreparation : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<RoomPreparation> _logger = ServiceLocator.GetLogger<RoomPreparation>();

    #region Service & State

    /// <summary>导出引用集合节点。</summary>
    public RoomPreparationInterRefs? InterRefs {
        get; private set;
    }
    /// <summary>副本资源表引用，解析副本显示名。</summary>
    [Export]
    private DungeonResourceTable? _dungeonResourceTable;
    /// <summary>当前选中的单位配置键（UI 瞬时态：选中待添加）。</summary>
    private string? _selectedUnitKey;

    /// <summary>客户端门面，房间会话的单一事实源，本面板仅读取展示。</summary>
    private static GameClientService Client => ServiceLocator.ClientService;

    #endregion

    /// <summary>
    /// 节点就绪：绑定按钮与单位选择事件，订阅准备阶段单位列表与准备状态推送。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<RoomPreparationInterRefs>("RoomPreparationInterRefs");
        if (InterRefs is null) {
            _logger.LogError("RoomPreparationInterRefs node not found.");
            return;
        }

        if (_dungeonResourceTable == null)
            _logger.LogError("_dungeonResourceTable is not assigned!");

        InterRefs?.SelectUnitButton?.Pressed += () => {
            // UnitSelectPanel 为本面板的子节点覆盖层，直接打开而不隐藏本面板
            InterRefs?.UnitSelectPanel?.OpenPanelFrom();
        };
        InterRefs?.BackButton?.Pressed += OnBackButtonPressed;
        var startBtn = InterRefs?.StartBattleButton;
        if (startBtn is not null) {
            startBtn.Pressed += OnStartBattleClicked;
            startBtn.Disabled = true;
        }
        var removeBtn = InterRefs?.RemoveUnitButton;
        if (removeBtn is not null) {
            removeBtn.Pressed += OnRemoveUnitClicked;
            removeBtn.Disabled = true;
        }

        // 订阅 UnitSelectPanel 的选择信号
        if (InterRefs?.UnitSelectPanel is not null)
            InterRefs.UnitSelectPanel.UnitSelected += OnUnitSelectedFromPanel;

        // 订阅主线程派发的房间快照（服务端组装单发：准备状态 + 单位 + 房间信息）
        Client.OnRoomSnapshotUpdated += OnRoomSnapshotUpdated;
        // 战斗退出（LeaveRoom）后，房间已解散，返回来源面板（大厅）
        Client.OnRoomLeft += OnRoomLeft;

        _logger.LogInformation("RoomPreparation ready");
    }

    /// <summary>
    /// 返回按钮：通知服务端离开房间（准备阶段主动退出），随后返回来源面板。
    /// 服务端据此移除成员，并在房主退出时转让房主、最后一人退出时删除房间。
    /// </summary>
    private void OnBackButtonPressed() {
        // 准备阶段主动退出：仅在身处房间时才通知服务端，随后返回来源面板
        if (Client.CurrentRoomId != null)
            Client.RequestLeaveRoom();
        GoBack();
    }

    /// <summary>
    /// 由 GameLobby 调用，进入准备阶段。房间状态由客户端门面承担，
    /// 本方法仅做首屏渲染引导：乐观配置先行，权威快照未命中时保持占位。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="config">房间配置（可用于快照未到时的乐观展示）。</param>
    /// <param name="isHost">当前玩家是否为房主。</param>
    public void EnterRoom(string roomId, RoomListing? config = null, bool isHost = false) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("进入房间: {RoomId}, isHost={IsHost}", roomId, isHost);

        RenderRoomState(config);
        InterRefs?.StatusLabel?.Text = "请选择单位...";
        RefreshUnitGrid();
        RefreshActionButtons();
    }

    /// <summary>
    /// 单位选择面板选择回调，记录选中单位并添加到列表。
    /// </summary>
    /// <param name="unitConfigKey">单位配置键。</param>
    private void OnUnitSelectedFromPanel(string unitConfigKey) {
        // 已准备时禁止更改角色，服务端权威兜底，UI 亦拦截
        if (Client.IsCurrentUserReady) {
            InterRefs?.StatusLabel?.Text = "已准备，不能更改角色";
            return;
        }
        _selectedUnitKey = unitConfigKey;
        var config = UnitCatalog.GetByKey(unitConfigKey);
        if (config is not null)
            InterRefs?.StatusLabel?.Text = $"已选择: {config.ConfigKey}";
        AddUnit();
    }

    /// <summary>
    /// 添加当前选中单位：经门面发大厅准备阶段请求。
    /// </summary>
    private void AddUnit() {
        if (string.IsNullOrEmpty(_selectedUnitKey))
            return;

        var config = UnitCatalog.GetByKey(_selectedUnitKey);
        if (config is null)
            return;
        string configKey = config.ConfigKey;

        // 阵营选项键由副本配置提供，服务端据此解析实际阵营；当前单阵营取首选项
        string? dungeonKey = Client.CurrentRoomSnapshot?.DungeonKey;
        GameConfig.Models.DungeonConfig? dungeonConfig = DungeonRegistry.Instance.GetByKey(dungeonKey);
        IReadOnlyList<PlayerCampOption>? playerCampOptions = dungeonConfig?.PlayerCampOptions;
        string? campOptionKey = playerCampOptions is { Count: > 0 } ? playerCampOptions[0].Key : null;

        if (campOptionKey is not null) {
            Client.RequestPrepareAddUnit(configKey, campOptionKey);
        }
        else {
            // 副本未配置玩家阵营选项，无法解析实际阵营，服务端必然拒绝，提前终止
            _logger.LogWarning("副本 {DungeonKey} 未配置玩家阵营选项，无法添加单位 {UnitKey}", dungeonKey, configKey);
            InterRefs?.StatusLabel?.Text = "副本阵营配置缺失，无法选择角色";
            return;
        }

        InterRefs?.StatusLabel?.Text = $"请求创建 {configKey}...";
        RefreshActionButtons();
    }

    /// <summary>
    /// 取消当前已选单位：从权威快照取本人单位，经门面发大厅移除请求，等服务端广播回流刷新。
    /// </summary>
    private void OnRemoveUnitClicked() {
        var unit = Client.CurrentRoomSnapshot?.Units
            .FirstOrDefault(u => u.PlayerName == Client.PlayerName);
        if (unit is null) {
            InterRefs?.StatusLabel?.Text = "当前没有已选角色";
            return;
        }

        _selectedUnitKey = null;
        Client.RequestPrepareRemoveUnit(unit.UnitConfigKey);
        InterRefs?.StatusLabel?.Text = $"请求取消 {unit.UnitConfigKey}...";
    }

    /// <summary>
    /// 订阅到的房间快照更新（GameClientService 仅转发当前房间快照）。
    /// 事件视为刷新信号，展示数据一律读取客户端当前会话，避免串房。
    /// </summary>
    private void OnRoomSnapshotUpdated(string eventRoomId, RoomSnapshot snapshot) {
        RenderRoomState(null);
        RefreshUnitGrid();
        RefreshActionButtons();
        if (Client.CurrentRoomSnapshot is { } s)
            InterRefs?.StatusLabel?.Text = $"单位列表已更新 ({s.Units.Count})";
    }

    /// <summary>
    /// 战斗退出（LeaveRoom）回调：房间已解散，返回来源面板（大厅）。
    /// 该事件仅在离开当前房间时触发，本面板不再跟踪任何房间，直接回退。
    /// </summary>
    private void OnRoomLeft(string roomId) {
        GoBack();
    }

    /// <summary>节点退出场景树：退订事件。</summary>
    public override void _ExitTree() {
        Client.OnRoomSnapshotUpdated -= OnRoomSnapshotUpdated;
        Client.OnRoomLeft -= OnRoomLeft;
    }

    /// <summary>
    /// 渲染房间展示：优先读客户端当前房间权威快照，未命中时退回乐观配置。
    /// </summary>
    /// <param name="config">乐观配置；当前快照可用时忽略。</param>
    private void RenderRoomState(RoomListing? config) {
        var snapshot = Client.CurrentRoomSnapshot;
        string hostName, dungeonKey, description;
        int currentPlayers, maxPlayers;
        if (snapshot is not null) {
            hostName = snapshot.HostName;
            dungeonKey = snapshot.DungeonKey;
            currentPlayers = snapshot.CurrentPlayers;
            maxPlayers = snapshot.MaxPlayers;
            description = snapshot.Description;
        }
        else if (config is not null) {
            hostName = config.HostName;
            dungeonKey = config.DungeonKey;
            currentPlayers = config.CurrentPlayers;
            maxPlayers = config.MaxPlayers;
            description = config.Description;
        }
        else {
            hostName = "";
            dungeonKey = "";
            currentPlayers = 0;
            maxPlayers = 2;
            description = "";
        }

        UpdateRoomInfoLabels(hostName, dungeonKey, currentPlayers, maxPlayers);
        if (InterRefs?.InfoLabel != null)
            InterRefs.InfoLabel.Text = description;
    }

    /// <summary>
    /// 刷新副标题三标签并列显示：房主 / 副本名 / 人数。
    /// </summary>
    /// <param name="hostName">房主玩家名。</param>
    /// <param name="dungeonKey">副本键。</param>
    /// <param name="currentPlayers">房间当前玩家数。</param>
    /// <param name="maxPlayers">房间最大玩家数。</param>
    private void UpdateRoomInfoLabels(string hostName, string dungeonKey, int currentPlayers, int maxPlayers) {
        if (InterRefs?.HostLabel != null)
            InterRefs.HostLabel.Text = string.IsNullOrEmpty(hostName) ? "房主: --" : $"房主: {hostName}";
        string dungeonText = _dungeonResourceTable?.GetDisplayName(dungeonKey) ?? dungeonKey;
        if (InterRefs?.DungeonNameLabel != null)
            InterRefs.DungeonNameLabel.Text = string.IsNullOrEmpty(dungeonText) ? "副本: --" : $"副本: {dungeonText}";
        if (InterRefs?.PlayersLabel != null)
            InterRefs.PlayersLabel.Text = $"人数: {currentPlayers}/{maxPlayers}";
    }

    /// <summary>
    /// 按房间玩家刷新 UnitGrid 职业选择卡片。
    /// 已选择职业的玩家展示职业名，未选择的展示占位；已准备玩家卡片高亮。
    /// 玩家快照为空时退化为仅按已选单位归属的玩家列卡（处理 unit_list 早于 room_state 到达）。
    /// </summary>
    private void RefreshUnitGrid() {
        var cardGrid = InterRefs?.UnitCardGrid;
        if (cardGrid is null || InterRefs?.UnitCardScene is null)
            return;

        var snapshot = Client.CurrentRoomSnapshot;
        List<PlayerReadyDto> players;
        if (snapshot is not null) {
            if (snapshot.Players.Count > 0)
                players = [.. snapshot.Players];
            else {
                // 玩家快照为空时按已选单位归属列卡（处理 unit_list 早于 room_state 到达）
                players = [];
                foreach (var unit in snapshot.Units)
                    players.Add(new PlayerReadyDto(unit.PlayerName, false));
            }
        }
        else {
            // 快照未到：保底一张自己的占位卡，避免网格空白
            players = [new PlayerReadyDto(Client.PlayerName, false)];
        }

        // 清空旧卡片
        foreach (Node child in cardGrid.GetChildren())
            child.QueueFree();

        foreach (var player in players) {
            var card = InterRefs.UnitCardScene.Instantiate<UnitCard>();
            string? unitConfigKey = snapshot?.Units.FirstOrDefault(u => u.PlayerName == player.PlayerName)?.UnitConfigKey;

            if (unitConfigKey != null && UnitCatalog.GetByKey(unitConfigKey) is { } config) {
                // 已选择职业：展示职业名 + 玩家名 + 真实 HP 数值
                card.SetupUnit(config.ConfigKey, config.MaxHealth);
                card.SetUserName(player.PlayerName);
            }
            else {
                // 未选择职业或配置缺失：占位样式
                card.SetPlaceholder(player.PlayerName);
            }

            cardGrid.AddChild(card);
            // 高亮在加入场景树后设置，确保 _refs 已就绪、背景色即时生效
            card.SetSelected(player.Ready);
        }
    }

    /// <summary>
    /// 刷新操作按钮状态机：房主主按钮为"开始战斗"（本人已选单位且其他玩家全部准备才可用），
    /// 非房主为"准备"/"取消准备"；"取消角色"仅在本人已选单位且未准备时可用，与"选择角色"同锁。
    /// </summary>
    private void RefreshActionButtons() {
        if (InterRefs is null)
            return;

        bool isHost = Client.IsCurrentUserHost;
        bool isReady = Client.IsCurrentUserReady;
        bool hasSelectedUnit = Client.HasCurrentUserUnit;

        if (InterRefs.StartBattleButton is { } startBtn) {
            if (isHost) {
                startBtn.Text = "开始战斗";
                startBtn.Disabled = !hasSelectedUnit || !Client.OthersReady;
            }
            else {
                startBtn.Text = isReady ? "取消准备" : "准备";
                startBtn.Disabled = !hasSelectedUnit;
            }
        }

        // 已准备锁定角色增删：服务端权威兜底，UI 同步禁用入口
        if (InterRefs.SelectUnitButton is { } selectBtn)
            selectBtn.Disabled = isReady;
        if (InterRefs.RemoveUnitButton is { } removeBtn)
            removeBtn.Disabled = !hasSelectedUnit || isReady;
    }

    /// <summary>
    /// 点击底部主按钮：房主触发开始战斗，非房主切换准备/取消准备。
    /// </summary>
    private void OnStartBattleClicked() {
        if (Client.IsCurrentUserHost)
            OnStartBattleAsHost();
        else
            OnToggleReady();
    }

    /// <summary>
    /// 房主点击开始战斗：校验单位与全员准备后，经门面发送大厅请求。
    /// </summary>
    private void OnStartBattleAsHost() {
        if (!Client.HasCurrentUserUnit) {
            InterRefs?.StatusLabel?.Text = "请先选择角色！";
            return;
        }

        if (!Client.OthersReady) {
            InterRefs?.StatusLabel?.Text = "等待其他玩家准备...";
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("请求开始战斗: {RoomId}, units={UnitCount}",
                Client.CurrentRoomId, Client.CurrentRoomSnapshot?.Units.Count ?? 0);

        // prepare_start_battle 走大厅链路，房间由服务端从连接归属反查。
        Client.RequestPrepareStartBattle();
    }

    /// <summary>
    /// 非房主点击准备/取消准备：发送切换请求，等待服务端广播确认。
    /// </summary>
    private void OnToggleReady() {
        if (!Client.IsConnected)
            return;

        if (Client.IsCurrentUserReady) {
            Client.RequestPrepareUnready();
            InterRefs?.StatusLabel?.Text = "已取消准备";
        }
        else {
            if (!Client.HasCurrentUserUnit) {
                InterRefs?.StatusLabel?.Text = "请先选择角色！";
                return;
            }
            Client.RequestPrepareReady();
            InterRefs?.StatusLabel?.Text = "已请求准备...";
        }
    }

}
