using Godot;

namespace DungeonChessBattle;

public partial class SkillRangeRect_Hint : Node3D {
    [Export]
    MeshInstance3D decalRef;

    ShaderMaterial shaderMaterial;

    public void Init(Vector3 fromPos, Vector3 toPos, float near, float far, float fromL, float toR) {
        shaderMaterial = decalRef.MaterialOverride as ShaderMaterial;
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
