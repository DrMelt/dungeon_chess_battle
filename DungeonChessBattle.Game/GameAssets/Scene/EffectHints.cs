using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 技能效果提示管理器，持有圆形/矩形范围提示场景资源。
/// </summary>
public partial class EffectHints : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<EffectHints> _logger = ServiceLocator.GetLogger<EffectHints>();

    /// <summary>圆形范围提示使用的场景资源。</summary>
    [Export]
    private PackedScene? _effectCircleRange_PKS;

    /// <summary>矩形范围提示使用的场景资源。</summary>
    [Export]
    private PackedScene? _effectRectRange_PKS;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (_effectCircleRange_PKS == null)
            _logger.LogError("_effectCircleRange_PKS is not assigned!");
        if (_effectRectRange_PKS == null)
            _logger.LogError("_effectRectRange_PKS is not assigned!");
    }

}
