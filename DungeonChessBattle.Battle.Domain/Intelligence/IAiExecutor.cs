using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain.Intelligence;

/// <summary>
/// AI 动作执行器：单位自治决策后经本接口向世界请求动作，由战斗世界实现并在 AddUnit 时注入单位。
/// 单位不感知场景实现细节；未绑定执行器时单位不动作。日志与权威校验由实现方承担。
/// </summary>
public interface IAiExecutor {
    /// <summary>应用移动意图：写入移动输入并按世界规则处理"移动即打断读条"；零向量表示静止，不打断读条。</summary>
    void SetMovement(IBattleUnit unit, Vector2 moveDirection);

    /// <summary>请求施法：按技能目标类型解析单位目标并校验发起读条；失败日志由实现方记录。</summary>
    void RequestCast(IBattleUnit caster, SkillKeyId skillKey, ushort targetNetId, Vector2 targetPosition);
}
