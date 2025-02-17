using UnityEngine;

public static class AngleUtils
{
    public static float ClampAngle(float angle, float min = -360f, float max = 360f)
    {
        do
        {
            if (angle < -360f)
            {
                angle += 360f;
            }
            if (angle > 360f)
            {
                angle -= 360f;
            }
        } while (angle < -360f || angle > 360f);

        return Mathf.Clamp(angle, min, max);
    }
}