using System.Numerics;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Replay;

/// <summary>回放输入类型。</summary>
public enum ReplayInputKind {
    /// <summary>移动方向意图段。</summary>
    Move,
    /// <summary>施法请求。</summary>
    Cast,
    /// <summary>聚焦请求。</summary>
    Focus,
}

/// <summary>
/// 输入条目：三类归档条目在单一帧轴上的统一形状，只承载事实不带文案，文案归表现层。
/// Frame 是绝对逻辑帧，与归档同数轴，换算相对时间由调用方减 <c>StartTick</c>。
/// 与类型无关的字段留默认值：<see cref="Length"/> 单帧条目恒为 1，
/// <see cref="MoveDir"/> 只对移动有意义，<see cref="SkillKey"/>/<see cref="TargetNetId"/>/<see cref="TargetPos"/>
/// 只对施法与聚焦有意义。<see cref="MoveDir"/> 与 <see cref="TargetPos"/> 同为 XZ 平面向量，
/// Y 分量即归档的 DirY 与 TargetPosZ；<see cref="TargetNetId"/> 为 0 表示无目标单位。
/// </summary>
public readonly record struct ReplayInputEntry(
    int Frame,
    int Length,
    ReplayInputKind Kind,
    byte PlayerIndex,
    bool Accepted,
    string SkillKey,
    ushort TargetNetId,
    Vector2 MoveDir,
    Vector2 TargetPos);

/// <summary>
/// 输入时间轴：三类条目按帧升序混排后的只读投影，供表现层定位当前帧前后的输入。
/// 不参与注入与游标推进，重放语义仍全在 <see cref="ReplayEngine"/>；构建一次即随回放不变。
/// 同帧条目按 施法 → 移动 → 聚焦 落序，由稳定排序保持追加序，与引擎注入序一致。
/// </summary>
public sealed class ReplayInputTimeline {
    private readonly ReplayInputEntry[] _entries;

    /// <summary>按帧升序的全部输入条目。</summary>
    public IReadOnlyList<ReplayInputEntry> Entries => _entries;

    private ReplayInputTimeline(ReplayInputEntry[] entries) => _entries = entries;

    /// <summary>把录制记录摊平成时间轴：只重排不改写，帧号与归档同数轴。</summary>
    public static ReplayInputTimeline Build(ReplayRecording recording) {
        int runCount = 0;
        foreach (var track in recording.MoveTracks)
            runCount += track.Runs.Count;

        var entries = new List<ReplayInputEntry>(recording.Casts.Count + runCount + recording.Focuses.Count);
        foreach (var cast in recording.Casts)
            entries.Add(new ReplayInputEntry(cast.Frame, 1, ReplayInputKind.Cast, cast.PlayerIndex, cast.Accepted,
                cast.SkillKey, cast.TargetNetId, Vector2.Zero, new Vector2(cast.TargetPosX, cast.TargetPosZ)));
        foreach (var track in recording.MoveTracks) {
            foreach (var run in track.Runs)
                entries.Add(new ReplayInputEntry(run.Frame, run.Length, ReplayInputKind.Move, track.PlayerIndex,
                    true, string.Empty, 0, new Vector2(run.DirX, run.DirY), Vector2.Zero));
        }
        foreach (var focus in recording.Focuses)
            entries.Add(new ReplayInputEntry(focus.Frame, 1, ReplayInputKind.Focus, focus.PlayerIndex, focus.Accepted,
                string.Empty, focus.TargetNetId, Vector2.Zero, Vector2.Zero));

        return new ReplayInputTimeline([.. entries.OrderBy(e => e.Frame)]);
    }

    /// <summary>最后一个帧号不晚于给定帧的条目下标，早于全部条目时返回 -1。</summary>
    public int IndexOfFrameAtOrBefore(int absoluteFrame) {
        int low = 0;
        int high = _entries.Length - 1;
        int found = -1;
        while (low <= high) {
            int mid = (low + high) / 2;
            if (_entries[mid].Frame <= absoluteFrame) {
                found = mid;
                low = mid + 1;
            }
            else {
                high = mid - 1;
            }
        }

        return found;
    }

    /// <summary>第一个帧号晚于给定帧的条目下标，晚于全部条目时返回 -1。</summary>
    public int IndexOfFrameAfter(int absoluteFrame) {
        int index = IndexOfFrameAtOrBefore(absoluteFrame) + 1;
        return index < _entries.Length ? index : -1;
    }
}
