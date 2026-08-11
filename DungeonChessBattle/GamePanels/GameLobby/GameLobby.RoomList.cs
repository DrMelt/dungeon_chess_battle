using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.Protocol.Enums;
using DungeonChessBattle.Protocol.Dtos;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// GameLobby 的房间列表（招募板）UI 处理：列表刷新、卡片创建、选中与状态文字。
/// </summary>
public partial class GameLobby {
    #region Room List UI

    /// <summary>
    /// 刷新房间列表 UI：移除已消失的房间，添加或更新现存卡片。
    /// </summary>
    /// <param name="rooms">最新的房间列表。</param>
    private void RefreshRoomList(List<RoomListing> rooms) {
        if (InterRefs?.RoomListContainer == null)
            return;

        var currentRoomIds = rooms.Select(r => r.RoomId).ToHashSet();

        // 移除已不存在的房间
        var toRemove = new List<string>();
        foreach (var (roomId, _) in _roomInfoCache) {
            if (!currentRoomIds.Contains(roomId)) {
                toRemove.Add(roomId);
            }
        }
        foreach (var roomId in toRemove) {
            if (_roomInfoCache.TryGetValue(roomId, out var node)) {
                InterRefs.RoomListContainer.RemoveChild(node);
                node.QueueFree();
                _roomInfoCache.Remove(roomId);
            }
        }

        // 添加/更新房间卡片
        foreach (var room in rooms) {
            if (!_roomInfoCache.TryGetValue(room.RoomId, out var roomInfo)) {
                roomInfo = CreateRoomInfoCard(room.RoomId);
                InterRefs.RoomListContainer.AddChild(roomInfo);
                _roomInfoCache[room.RoomId] = roomInfo;
            }

            string statusText = GetRoomStatusText(room);
            roomInfo.UpdateStatus(statusText);
        }

        // 空状态提示
        if (rooms.Count == 0 && InterRefs?.DetailLabel != null) {
            InterRefs.DetailLabel.Text = "当前没有房间\n\n使用左侧面板创建一个房间吧！";
        }
    }

    /// <summary>
    /// 实例化并初始化单个房间卡片。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>创建好的房间卡片实例。</returns>
    private RoomInfo CreateRoomInfoCard(string roomId) {
        if (InterRefs?.RoomInfoScene is null)
            throw new System.InvalidOperationException("RoomInfoScene is not assigned.");
        var instance = InterRefs.RoomInfoScene.Instantiate<RoomInfo>();
        instance.Setup(roomId, "等待中");
        instance.RoomSelected += OnRoomSelected;
        return instance;
    }

    /// <summary>
    /// 房间卡片选中回调：更新选中高亮、详情面板并启用加入按钮。
    /// </summary>
    /// <param name="roomId">选中的房间 ID。</param>
    private void OnRoomSelected(string roomId) {
        // 取消上一个选中
        if (_selectedRoomId != null && _roomInfoCache.TryGetValue(_selectedRoomId, out var prev)) {
            prev.SetSelected(false);
        }

        _selectedRoomId = roomId;
        _selectedRoomConfig = null;

        // 高亮当前选中
        if (_roomInfoCache.TryGetValue(roomId, out var current)) {
            current.SetSelected(true);
        }

        // 更新详情面板并从缓存的 listing 中获取配置
        if (InterRefs?.DetailLabel != null) {
            var listing = _lastRoomListings?.FirstOrDefault(r => r.RoomId == roomId);
            if (listing != null) {
                _selectedRoomConfig = listing;
                InterRefs.DetailLabel.Text = $"房间: {listing.Title}\n房主: {listing.HostName}\n人数: {listing.CurrentPlayers}/{listing.MaxPlayers}";
            }
            else {
                InterRefs.DetailLabel.Text = $"选中房间: {roomId}\n";
            }
        }

        // 启用加入按钮
        InterRefs?.JoinButton?.Disabled = false;
    }

    /// <summary>
    /// 生成房间状态文字（等待中 / 已结束）。
    /// </summary>
    /// <param name="room">房间实例。</param>
    /// <returns>状态文字。</returns>
    private static string GetRoomStatusText(RoomListing room) =>
        room.Status != RoomStatus.Finished ? "等待中" : "已结束";

    #endregion
}
