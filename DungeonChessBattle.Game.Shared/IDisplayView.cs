using Godot;

namespace DungeonChessBattle.Game.Shared;

/// <summary>
/// 展示层只读视图契约：内置 <c>.tres</c> 资源与 mod 运行时构造的展示数据共同实现的成员形状。
/// UI 与表现层面向本组契约取数，不认具体资源类；同键的 mod 视图覆盖内置视图。
/// 字符串成员未声明时为空串、资源成员未声明时为 null，回退值由消费方决定。
/// </summary>
public interface ISkillView {
    /// <summary>技能键，与 content.json 的 skills[].id 对齐。</summary>
    string Id {
        get;
    }

    /// <summary>技能名称，未声明为空串。</summary>
    string Name {
        get;
    }

    /// <summary>技能描述，未声明为空串。</summary>
    string Description {
        get;
    }

    /// <summary>技能图标，未配置或解析失败为 null。</summary>
    Texture2D? Icon {
        get;
    }

    /// <summary>施放特效场景模板，未配置为 null。</summary>
    PackedScene? ApplyEffectScene {
        get;
    }

    /// <summary>选位置目标时的范围提示场景模板，未配置为 null。</summary>
    PackedScene? RangeHintScene {
        get;
    }
}

/// <summary>Buff 展示视图。Buff 的领域侧身份只有同步数值 ID，故查询键即 BuffTypeId。</summary>
public interface IBuffView {
    /// <summary>跨端同步数值身份，展示查询主键。</summary>
    ushort BuffTypeId {
        get;
    }

    /// <summary>Buff 名称，未声明为空串。</summary>
    string Name {
        get;
    }

    /// <summary>Buff 描述，未声明为空串。</summary>
    string Description {
        get;
    }

    /// <summary>Buff 图标，未配置或解析失败为 null。</summary>
    Texture2D? Icon {
        get;
    }
}

/// <summary>副本展示视图。</summary>
public interface IDungeonView {
    /// <summary>副本键，与 content.json 的 dungeons[].key 对齐。</summary>
    string Key {
        get;
    }

    /// <summary>副本显示名，未声明为空串。</summary>
    string DisplayName {
        get;
    }

    /// <summary>副本描述，未声明为空串。</summary>
    string Description {
        get;
    }

    /// <summary>环境表现场景模板，主题已在场景内固化，未配置为 null 由消费方回退默认副本场景。</summary>
    PackedScene? EnvScene {
        get;
    }
}

/// <summary>
/// 单位展示视图：单位此前没有展示面，配置键被直接当显示名使用。
/// 仅覆盖展示字段，单位数值与行为仍在数据面 content.json。
/// </summary>
public interface IUnitView {
    /// <summary>单位配置键，与 content.json 的 units[].configKey 对齐。</summary>
    string ConfigKey {
        get;
    }

    /// <summary>单位显示名，未配置时由消费方回退配置键。</summary>
    string DisplayName {
        get;
    }

    /// <summary>单位描述。</summary>
    string Description {
        get;
    }

    /// <summary>单位图标，未配置或解析失败为 null。</summary>
    Texture2D? Icon {
        get;
    }

    /// <summary>单位模型场景模板，未配置为 null 由消费方回退内置共享模板。</summary>
    PackedScene? ModelScene {
        get;
    }

    /// <summary>单位主体配色，未配置为 null 由消费方保持模型原样。</summary>
    Color? BodyColor {
        get;
    }
}
