using System;
using System.Collections.Generic;
using DungeonChessBattle.Entities;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 场景单位集合资源，管理单位的增删并广播变化事件。
/// 数据源为网络同步 UnitPawn（LES SyncVar，客户端只读），UI 仅枚举展示，不驱动模拟。
/// </summary>
[GlobalClass]
public partial class UnitsInScene : Resource {
    /// <summary>
    /// 构造函数：初始化单位数组。
    /// </summary>
    public UnitsInScene() {
        unitsArr = [];
    }

    /// <summary>场景中的单位 Pawn 数组。</summary>
    private readonly List<UnitPawn> unitsArr = [];

    /// <summary>场景单位数组快照。</summary>
    public List<UnitPawn> UnitsArr => [.. unitsArr];

    /// <summary>房间创建时间（UTC，服务端权威，由 <see cref="SetRoomCreatedAt"/> 注入）。</summary>
    private DateTime _roomCreatedAt = DateTime.MinValue;

    /// <summary>
    /// 设置房间创建时间（服务端权威，来自 BattleRoomEntity.CreatedUnixTime 同步字段）。
    /// 进入战斗时由 BattleUnitManager.Bind 注入；退出战斗时传 <see cref="DateTime.MinValue"/> 重置。
    /// </summary>
    /// <param name="utc">房间创建时刻（UTC）。</param>
    public void SetRoomCreatedAt(DateTime utc) {
        _roomCreatedAt = utc;
    }

    /// <summary>
    /// 从房间创建时刻起经过的秒数（战斗计时，跨端一致）。
    /// 未设置创建时间（如未进入战斗）时返回 0。
    /// </summary>
    public double SceneTime => _roomCreatedAt == DateTime.MinValue
        ? 0.0
        : (DateTime.UtcNow - _roomCreatedAt).TotalSeconds;

    /// <summary>
    /// 添加单位。
    /// </summary>
    /// <param name="pawn">要添加的单位 Pawn。</param>
    public void AddUnit(UnitPawn pawn) {
        unitsArr.Add(pawn);
    }

    /// <summary>
    /// 移除单位。
    /// </summary>
    /// <param name="pawn">要移除的单位 Pawn。</param>
    public void RemoveUnit(UnitPawn pawn) {
        unitsArr.Remove(pawn);
    }

    /// <summary>
    /// 清空全部单位。
    /// </summary>
    public void RemoveAll() {
        unitsArr.Clear();
    }

}
