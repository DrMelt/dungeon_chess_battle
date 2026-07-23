using System;

namespace DungeonChessBattle.Core {
    public enum EnumCamp {
        None = 0,
        Camp_A,
        Camp_BOSS,
        EnumCampLength,
    }

    public enum Enum_DamageType {
        None = 0,
        Physcial,
        Magic,
    }

    [Flags]
    public enum EnumSkillCanAdd {
        None = 0,
        Same = 1,
        Different = 2,
    }
}
