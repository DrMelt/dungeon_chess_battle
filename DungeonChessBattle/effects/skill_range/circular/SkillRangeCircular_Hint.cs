using System;
using Godot;

namespace DungeonChessBattle;

public partial class SkillRangeCircular_Hint : Node3D {
    public SkillRangeCircular_HintInterRefs? InterRefs {
        get; private set;
    }

    ShaderMaterial shaderMaterial = null!;

    public override void _Ready() {
        InterRefs = GetNode<SkillRangeCircular_HintInterRefs>("SkillRangeCircular_HintInterRefs");
    }

    public void Init(Vector3 fromPos, Vector3 toPos, float near, float far, float radianFrom, float radianTo) {
        shaderMaterial = (InterRefs!.DecalRef.MaterialOverride as ShaderMaterial) ?? throw new InvalidOperationException("decalRef.MaterialOverride is not a ShaderMaterial.");

        GlobalPosition = fromPos;
        toPos.Y = GlobalPosition.Y;
        LookAt(toPos, up: Vector3.Up);

        InterRefs!.DecalRef.Scale = new Vector3(far * 2, 1, far * 2);

        shaderMaterial.SetShaderParameter("Near", near);
        shaderMaterial.SetShaderParameter("Skill_Radian_From", radianFrom);
        shaderMaterial.SetShaderParameter("Skill_Radian_To", radianTo);
    }

    public void SetSkillProgress(float progress) {
        shaderMaterial.SetShaderParameter("Skill_Progress", progress);
    }


}
