using System;
using Godot;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 矩形范围技能提示，通过着色器展示矩形区域范围，并支持施法进度显示。
/// </summary>
public partial class SkillRangeRect_Hint : Node3D {
    /// <summary>
    /// 导出引用集合节点。
    /// </summary>
    public SkillRangeRect_HintInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 范围提示着色器材质。
    /// </summary>
    private ShaderMaterial? shaderMaterial;

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillRangeRect_HintInterRefs>("SkillRangeRect_HintInterRefs");
    }

    /// <summary>
    /// 初始化提示：设置位置朝向、矩形尺寸与近端距离。
    /// </summary>
    /// <param name="fromPos">技能施放起点。</param>
    /// <param name="toPos">技能目标方向。</param>
    /// <param name="near">近端距离。</param>
    /// <param name="far">远端距离。</param>
    /// <param name="fromL">矩形左边界。</param>
    /// <param name="toR">矩形右边界。</param>
    public void Init(Vector3 fromPos, Vector3 toPos, float near, float far, float fromL, float toR) {
        var interRefs = InterRefs ?? throw new InvalidOperationException("InterRefs has not been initialized.");
        var decalRef = interRefs.DecalRef ?? throw new InvalidOperationException("DecalRef is not assigned.");
        shaderMaterial = (decalRef.MaterialOverride as ShaderMaterial) ?? throw new InvalidOperationException("decalRef.MaterialOverride is not a ShaderMaterial.");
        GlobalPosition = fromPos;
        toPos.Y = fromPos.Y;
        LookAt(toPos, up: Vector3.Up);

        Scale = new Vector3(toR - fromL, 1, far);
        var dPos = decalRef.Position;
        dPos.X = (toR + fromL) * 0.5f;
        decalRef.Position = dPos;

        shaderMaterial.SetShaderParameter("Near", near);
    }

    /// <summary>
    /// 更新技能施法进度显示。
    /// </summary>
    /// <param name="progress">施法进度（0~1）。</param>
    public void SetSkillProgress(float progress) {
        shaderMaterial?.SetShaderParameter("Skill_Progress", progress);
    }
}
