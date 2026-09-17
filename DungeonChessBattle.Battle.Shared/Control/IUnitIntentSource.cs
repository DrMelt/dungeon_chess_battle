namespace DungeonChessBattle.Battle.Shared.Control;

/// <summary>
/// 单位意图源：驱动一个单位的意图产出者，玩家输入与自治决策同形。
/// 每单位一份实例，持该单位的当帧产出；刷新时机一律由驱动侧单点调用，实现不自持节拍。
/// </summary>
public interface IUnitIntentSource {
    /// <summary>刷新：消费本帧输入与待决意图，产出当帧意图。每逻辑帧一次。</summary>
    /// <param name="deltaTime">距上一逻辑帧的间隔秒数。</param>
    void Refresh(float deltaTime);

    /// <summary>当帧意图，刷新之后读取。</summary>
    UnitIntent Intent {
        get;
    }
}
