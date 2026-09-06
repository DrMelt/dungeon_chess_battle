using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 地牢环境场景根节点，承载地面、光照与天空表现。
/// 主题固化在场景模板内：每个副本的 EnvScene 即成品场景，加载直接正确，运行时不设置主题。
/// 由 DungeonResourceTable.InstantiateEnvironment 按副本键实例化，
/// BattleCoordinator 挂载与按会话键重建，退出战斗即销毁。
/// </summary>
public partial class DungeonEnv : Node3D;
