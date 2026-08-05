using System;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 圆形范围技能提示，通过着色器展示近/远距离扇形范围。
/// </summary>
public partial class SkillRangeCircular_Hint : Node3D {
    /// <summary>导出引用集合节点。</summary>
    public SkillRangeCircular_HintInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>范围提示着色器材质。</summary>
    private ShaderMaterial? shaderMaterial;

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillRangeCircular_HintInterRefs>("SkillRangeCircular_HintInterRefs");
    }

    /// <summary>
    /// 初始化提示：设置位置朝向、扇形尺寸与角度范围。
    /// </summary>
    /// <param name="fromPos">技能施放起点。</param>
    /// <param name="toPos">技能目标方向。</param>
    /// <param name="near">近端距离。</param>
    /// <param name="far">远端距离。</param>
    /// <param name="radianFrom">扇形起始弧度。</param>
    /// <param name="radianTo">扇形结束弧度。</param>
    public void Init(Vector3 fromPos, Vector3 toPos, float near, float far, float radianFrom, float radianTo) {
        var interRefs = InterRefs ?? throw new InvalidOperationException("InterRefs has not been initialized.");
        var decalRef = interRefs.DecalRef ?? throw new InvalidOperationException("DecalRef is not assigned.");
        shaderMaterial = (decalRef.MaterialOverride as ShaderMaterial) ?? throw new InvalidOperationException("decalRef.MaterialOverride is not a ShaderMaterial.");

        GlobalPosition = fromPos;
        toPos.Y = GlobalPosition.Y;
        LookAt(toPos, up: Vector3.Up);

        decalRef.Scale = new Vector3(far * 2, 1, far * 2);

        shaderMaterial.SetShaderParameter("Near", near);
        shaderMaterial.SetShaderParameter("Skill_Radian_From", radianFrom);
        shaderMaterial.SetShaderParameter("Skill_Radian_To", radianTo);
    }

    /// <summary>
    /// 更新技能施法进度显示。
    /// </summary>
    /// <param name="progress">施法进度（0~1）。</param>
    public void SetSkillProgress(float progress) {
        shaderMaterial?.SetShaderParameter("Skill_Progress", progress);
    }
}
