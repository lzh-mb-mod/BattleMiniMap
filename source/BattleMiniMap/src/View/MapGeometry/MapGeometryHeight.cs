using System;
namespace BattleMiniMap.View.MapGeometry
{
    internal static class MapGeometryHeight
    {
        public static float? Ceiling(bool enabled,float height) =>
            enabled && !float.IsNaN(height) && !float.IsInfinity(height)
                ? (float?)Math.Max(-50,Math.Min(1000,height)) : null;
    }
}
