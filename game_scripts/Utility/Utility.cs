using Godot;
using System;

public class Utility
{

    #region IsInRange_Circular

    static public bool IsInRange_Circular(Vector3 checkedPos, Vector3 pinPos, Vector3 pinDir, float farClamp, float nearClamp, float radianFrom = -Mathf.Pi, float radianTo = Mathf.Pi)
    {
        return IsInRange_Circular(new Vector2(checkedPos.X, checkedPos.Z), new Vector2(pinPos.X, pinPos.Z), new Vector2(pinDir.X, pinDir.Z), farClamp, nearClamp, radianFrom, radianTo);
    }

    static public bool IsInRange_Circular(Vector2 checkedPos, Vector2 pinPos, Vector2 pinDir, float farClamp, float nearClamp, float radianFrom = -Mathf.Pi, float radianTo = Mathf.Pi)
    {
        pinDir = pinDir.Normalized();

        Vector2 dirToCheck = (checkedPos - pinPos).Normalized();


        var tan_x = dirToCheck.Dot(pinDir);
        var tan_y = dirToCheck.Cross(pinDir);

        float angle = Mathf.Atan2(tan_y, tan_x);

        if (angle >= radianFrom && angle <= radianTo)
        {
            float distanceSquared = pinPos.DistanceSquaredTo(checkedPos);

            if (distanceSquared >= nearClamp * nearClamp && distanceSquared <= farClamp * farClamp)
            {
                return true;
            }
        }

        return false;
    }

    #endregion



    static public bool IsInRange_Rect(Vector2 pinPos, Vector2 checkedPos, Vector2 pinDir, float nearClamp, float farClamp, float fromLeft, float toRight)
    {
        pinDir = pinDir.Normalized();
        Vector2 toCheck = checkedPos - pinPos;

        float tan_x = toCheck.Dot(pinDir);
        float tan_y = toCheck.Cross(pinDir);

        if (tan_y >= nearClamp && tan_y <= farClamp &&
            tan_x >= fromLeft && tan_x <= toRight)
        {
            return true;
        }

        return false;
    }
}
