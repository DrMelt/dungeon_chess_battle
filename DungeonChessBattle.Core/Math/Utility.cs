using System;
using System.Numerics;

namespace DungeonChessBattle.Core {
    public class Utility {
        public static bool IsInRange_Circular(Vector3 checkedPos, Vector3 pinPos, Vector3 pinDir, float nearClamp, float farClamp, float radianFrom = -MathF.PI, float radianTo = MathF.PI) {
            return IsInRange_Circular(new Vector2(checkedPos.X, checkedPos.Z), new Vector2(pinPos.X, pinPos.Z), new Vector2(pinDir.X, pinDir.Z), nearClamp, farClamp, radianFrom, radianTo);
        }

        public static bool IsInRange_Circular(Vector2 checkedPos, Vector2 pinPos, Vector2 pinDir, float nearClamp, float farClamp, float radianFrom = -MathF.PI, float radianTo = MathF.PI) {
            pinDir = Vector2.Normalize(pinDir);

            Vector2 dirToCheck = Vector2.Normalize(checkedPos - pinPos);

            var tan_x = Vector2.Dot(dirToCheck, pinDir);
            var tan_y = VectorMath.Cross(dirToCheck, pinDir);

            float angle = MathF.Atan2(tan_y, tan_x);

            if (angle >= radianFrom && angle <= radianTo) {
                float distanceSquared = Vector2.DistanceSquared(checkedPos, pinPos);

                if (distanceSquared >= nearClamp * nearClamp && distanceSquared <= farClamp * farClamp) {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInRange_Rect(Vector3 checkedPos, Vector3 pinPos, Vector3 pinDir, float nearClamp, float farClamp, float fromL, float toR) {
            return IsInRange_Rect(new Vector2(checkedPos.X, checkedPos.Z), new Vector2(pinPos.X, pinPos.Z), new Vector2(pinDir.X, pinDir.Z), nearClamp, farClamp, fromL, toR);
        }

        public static bool IsInRange_Rect(Vector2 checkedPos, Vector2 pinPos, Vector2 pinDir, float nearClamp, float farClamp, float fromLeft, float toRight) {
            pinDir = Vector2.Normalize(pinDir);
            Vector2 toCheck = checkedPos - pinPos;

            float tan_x = Vector2.Dot(toCheck, pinDir);
            float tan_y = VectorMath.Cross(toCheck, pinDir);

            if (tan_x >= nearClamp && tan_x <= farClamp &&
                tan_y >= fromLeft && tan_y <= toRight) {
                return true;
            }

            return false;
        }
    }
}
