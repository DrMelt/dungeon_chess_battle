using System;
using Godot;

namespace DungeonChessBattle;

public partial class SkillRangeRect_Hint : Node3D {
    public SkillRangeRect_HintInterRefs? InterRefs {
        get; private set;
    }

    ShaderMaterial? shaderMaterial;

    public override void _Ready() {
        InterRefs = GetNode<SkillRangeRect_HintInterRefs>("SkillRangeRect_HintInterRefs");
    }

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


    public void SetSkillProgress(float progress) {
        shaderMaterial?.SetShaderParameter("Skill_Progress", progress);
    }
}
