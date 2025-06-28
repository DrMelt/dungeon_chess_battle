using GameLogic;
using Godot;
using System;

public partial class SkillRangeCircular_Hint : Node3D {
    [Export]
    MeshInstance3D decalRef;

    ShaderMaterial shaderMaterial;


    public void Init(Vector3 fromPos, Vector3 toPos, float near, float far, float radianFrom, float radianTo) {
        shaderMaterial = decalRef.MaterialOverride as ShaderMaterial;

        GlobalPosition = fromPos;
        toPos.Y = GlobalPosition.Y;
        LookAt(toPos, up: Vector3.Up);

        decalRef.Scale = new Vector3(far * 2, 1, far * 2);

        shaderMaterial.SetShaderParameter("Near", near);
        shaderMaterial.SetShaderParameter("Skill_Radian_From", radianFrom);
        shaderMaterial.SetShaderParameter("Skill_Radian_To", radianTo);
    }

    public void SetSkillProgress(float progress) {
        shaderMaterial.SetShaderParameter("Skill_Progress", progress);
    }


}
