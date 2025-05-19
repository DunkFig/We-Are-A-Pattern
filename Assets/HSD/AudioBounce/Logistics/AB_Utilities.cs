using UnityEngine;
using Color = System.Drawing.Color;

namespace HSD.AudioBounce.Utilities
{
    public static class AB_Utilities
    { 
        public static Color32 ToColor32(Color color)
        {
            return new Color32(color.R, color.G, color.B, color.A);
        }
        
        public static Color32 ToColor32(Color color, float alpha)
        {
            return new Color32(color.R, color.G, color.B, (byte)(alpha * 255));
        }
        
        //UTILITY FUNCTIONS

        public static void DrawSemiCircleGizmo(Transform transform, int segments, float radius, Color color)
        {
            int semiCircleSegments = segments;
            float semiCircleRadius = radius;
            Gizmos.color = ToColor32(color);
            var transform1 = transform;
            Vector3 forward = transform1.forward * semiCircleRadius;
            Vector3 previousPoint = transform1.position + Quaternion.Euler(0, -90, 0) * forward;

            for (int i = 1; i <= semiCircleSegments; i++)
            {
                float angle = Mathf.Lerp(-90, 90, (float)i / semiCircleSegments);
                Vector3 currentPoint = transform.position + Quaternion.Euler(0, angle, 0) * forward;
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }
    }
}
