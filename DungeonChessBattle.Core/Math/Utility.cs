using System.Numerics;

namespace DungeonChessBattle.Core.Math;

public class Utility {
    public static bool IsInRange_Circular(Vector3 checkedPos, Vector3 pinPos, Vector3 pinDir, float nearClamp, float farClamp, float radianFrom = -MathF.PI, float radianTo = MathF.PI, float bodyRadius = 0f) {
        return IsInRange_Circular(new Vector2(checkedPos.X, checkedPos.Z), new Vector2(pinPos.X, pinPos.Z), new Vector2(pinDir.X, pinDir.Z), nearClamp, farClamp, radianFrom, radianTo, bodyRadius);
    }

    public static bool IsInRange_Circular(Vector2 checkedPos, Vector2 pinPos, Vector2 pinDir, float nearClamp, float farClamp, float radianFrom = -MathF.PI, float radianTo = MathF.PI, float bodyRadius = 0f) {
        pinDir = Vector2.Normalize(pinDir);

        Vector2 toCheck = checkedPos - pinPos;
        float d = toCheck.Length();

        // 距离条件：圆与环形 [nearClamp, farClamp] 有交集
        if (d + bodyRadius < nearClamp || d - bodyRadius > farClamp) {
            return false;
        }

        // 角度条件：圆包含原点时角度条件恒成立
        if (d <= bodyRadius) {
            return true;
        }

        // 圆心方向角度，以及圆对原点张角半宽
        Vector2 dirToCheck = toCheck / d;
        float tan_x = Vector2.Dot(dirToCheck, pinDir);
        float tan_y = VectorMath.Cross(dirToCheck, pinDir);
        float centerAngle = MathF.Atan2(tan_y, tan_x);
        float halfAngle = MathF.Asin(MathF.Min(bodyRadius / d, 1f));

        // 圆心角度覆盖区间 [centerAngle - halfAngle, centerAngle + halfAngle] 与 [radianFrom, radianTo] 有交集
        if (AngularIntervalsOverlap(centerAngle - halfAngle, centerAngle + halfAngle, radianFrom, radianTo)) {
            return true;
        }

        return false;
    }

    public static bool IsInRange_Rect(Vector3 checkedPos, Vector3 pinPos, Vector3 pinDir, float nearClamp, float farClamp, float fromL, float toR, float bodyRadius = 0f) {
        return IsInRange_Rect(new Vector2(checkedPos.X, checkedPos.Z), new Vector2(pinPos.X, pinPos.Z), new Vector2(pinDir.X, pinDir.Z), nearClamp, farClamp, fromL, toR, bodyRadius);
    }

    public static bool IsInRange_Rect(Vector2 checkedPos, Vector2 pinPos, Vector2 pinDir, float nearClamp, float farClamp, float fromLeft, float toRight, float bodyRadius = 0f) {
        pinDir = Vector2.Normalize(pinDir);
        Vector2 toCheck = checkedPos - pinPos;

        float tan_x = Vector2.Dot(toCheck, pinDir);
        float tan_y = VectorMath.Cross(toCheck, pinDir);

        float closestX = System.Math.Clamp(tan_x, nearClamp, farClamp);
        float closestY = System.Math.Clamp(tan_y, fromLeft, toRight);
        float dx = tan_x - closestX;
        float dy = tan_y - closestY;

        return dx * dx + dy * dy <= bodyRadius * bodyRadius;
    }

    /// <summary>
    /// 判断两个角度区间（弧度制，区间边界可能超出 [-π, π]）是否有交集。
    /// </summary>
    private static bool AngularIntervalsOverlap(float aFrom, float aTo, float bFrom, float bTo) {
        float twoPi = 2f * MathF.PI;
        aFrom = NormalizeAnglePositive(aFrom);
        aTo = NormalizeAnglePositive(aTo);
        bFrom = NormalizeAnglePositive(bFrom);
        bTo = NormalizeAnglePositive(bTo);

        float aLen = aTo - aFrom;
        if (aLen < 0f)
            aLen += twoPi;
        float bLen = bTo - bFrom;
        if (bLen < 0f)
            bLen += twoPi;

        float distCW = bFrom - aFrom;
        if (distCW < 0f)
            distCW += twoPi;

        return distCW <= aLen || distCW + bLen <= aLen + twoPi;
    }

    private static float NormalizeAnglePositive(float angle) {
        float twoPi = 2f * MathF.PI;
        angle %= twoPi;
        if (angle < 0f)
            angle += twoPi;
        return angle;
    }
}
