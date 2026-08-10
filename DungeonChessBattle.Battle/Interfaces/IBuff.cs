namespace DungeonChessBattle.Battle.Interfaces {
    /// <summary>
    /// Buff 接口，定义持续生效的增益/减益效果（如 HOT、DOT）。
    /// </summary>
    public interface IBuff {
        /// <summary>Buff 全局唯一 ID（对应配置表与 SyncBuffData.BuffTypeId）。</summary>
        ushort BuffTypeId {
            get;
        }

        /// <summary>Buff 名称，作为叠加判定的唯一标识。</summary>
        string BuffName {
            get;
        }

        /// <summary>剩余持续时间（秒）。</summary>
        double Duration {
            get;
        }

        /// <summary>当前叠加层数。</summary>
        int Superpositions {
            get;
        }

        /// <summary>最大可叠加层数。</summary>
        int MaxSuperpositions {
            get;
        }

        /// <summary>是否仍生效。失效后将被从单位的 Buff 列表移除。</summary>
        bool IsAlive {
            get;
        }

        /// <summary>释放该 Buff 的施法单位，可能为 null。</summary>
        IUnitState? FromUnit {
            get;
        }

        /// <summary>
        /// 按帧推进 Buff 效果（处理 duration 计时、持续效果与结束逻辑）。
        /// </summary>
        /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
        /// <param name="unitState">承载该 Buff 的目标单位。</param>
        void Update(double deltaTime, IUnitState unitState);

        /// <summary>
        /// 叠加另一层同类型 Buff，更新层数与持续时间。
        /// </summary>
        /// <param name="other">用于叠加的另一个 Buff 实例。</param>
        void AddSuperpositions(IBuff other);
    }
}
