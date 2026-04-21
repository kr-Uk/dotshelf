using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public static class BookFootprintUtility
    {
        public static float GetFootprint(float thickness, float depth, BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return thickness;
                case BookOrientation.Front:
                    return depth;
                case BookOrientation.Angled45:
                    return Mathf.Abs(thickness * Mathf.Cos(Mathf.Deg2Rad * 45f)) +
                           Mathf.Abs(depth * Mathf.Sin(Mathf.Deg2Rad * 45f));
                default:
                    return thickness;
            }
        }

        public static Quaternion GetVisualRotation(BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return Quaternion.Euler(0f, 0f, 0f);
                case BookOrientation.Front:
                    return Quaternion.Euler(0f, 90f, 0f);
                case BookOrientation.Angled45:
                    return Quaternion.Euler(0f, 45f, 0f);
                default:
                    return Quaternion.identity;
            }
        }
    }
}
