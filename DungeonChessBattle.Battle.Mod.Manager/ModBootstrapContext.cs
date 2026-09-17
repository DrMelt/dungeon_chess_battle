using DungeonChessBattle.Battle.Config.Registry;
using DungeonChessBattle.Battle.Config.Shared.Buffs;
using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Config.Shared.Content;
using DungeonChessBattle.Battle.Mod.Shared;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 引导上下文实现：内容注册转发 <see cref="ContentSetRegistry"/>，日志工厂原样交出。
/// mod 数据代码入口经本上下文把领域定义对象写进注册表，内容全部来自 mod。
/// 本类只做转发与失败归属：写入规则一律落在注册表，注册表以错误返回空键拒绝，
/// 这里把它连同当前 mod ID 收成 <see cref="ModError"/> 交装配方，mod 侧签名保持无返回值。
/// </summary>
public sealed class ModBootstrapContext(ContentSetRegistry registry, ILoggerFactory loggerFactory)
    : IModBootstrapContext {
    private readonly List<ModError> _errors = [];
    private string _modId = string.Empty;

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory {
        get;
    } = loggerFactory;

    /// <summary>本次装配累计的注册失败，按归属 mod 逐条带原因。</summary>
    public IReadOnlyList<ModError> Errors => _errors;

    /// <summary>标记接下来的注册归属该 mod，由装配方在每个入口执行前设置。</summary>
    public void BeginMod(string modId) => _modId = modId;

    /// <inheritdoc/>
    public void RegisterSkill(SkillDefinition skill) =>
        Collect(registry.RegisterSkill(skill));

    /// <inheritdoc/>
    public void RegisterBuff(BuffDefinition buff) =>
        Collect(registry.RegisterBuff(buff));

    /// <inheritdoc/>
    public void RegisterUnit(UnitConfig unit) =>
        Collect(registry.RegisterUnit(unit));

    /// <inheritdoc/>
    public void RegisterPlayerSelectableUnit(UnitConfig unit) =>
        Collect(registry.RegisterPlayerSelectableUnit(unit));

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonConfig dungeon) =>
        Collect(registry.RegisterDungeon(dungeon));

    private void Collect(ErrorOr<Success> result) {
        if (result.IsError)
            _errors.Add(new ModError(_modId, result.FirstError.Description));
    }
}
