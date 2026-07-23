using System;

namespace DungeonChessBattle.Core.Interfaces {
    public interface IBuff {
        string BuffName {
            get;
        }
        string BuffDescription {
            get;
        }
        string IconPath {
            get;
        }
        double Duration {
            get;
        }
        int Superpositions {
            get;
        }
        int MaxSuperpositions {
            get;
        }
        bool IsAlive {
            get;
        }
        IUnitState FromUnit {
            get;
        }

        void Update(double deltaTime, IUnitState unitState);
        void AddSuperpositions(IBuff other);
    }
}
