using System;

namespace BattleMiniMap.View.MapGeometry
{
    // Same UV domain as GDI and markers, expressed in collector-normalized units.
    internal sealed class MapRuntimeMainProjection
    {
        public readonly GeometryPoint Eye;
        public readonly float Right, Bottom, Near, Far;
        public MapRuntimeMainProjection(MapLiveGeometryRules rules, float minX, float minY,
            float resolution, int width, int height, float lowest, float highest, float? ceiling)
        {
            if (!MapLiveGeometryRules.Finite(lowest) || !MapLiveGeometryRules.Finite(highest) || highest < lowest)
                throw new ArgumentException("Invalid runtime height range");
            var original = new MapGeometryProjection(minX, minY, resolution, width, height, lowest, highest);
            Eye = rules.Normalize(new GeometryPoint(minX, minY, original.CameraZ));
            Right = original.Right / rules.Scale; Bottom = original.Bottom / rules.Scale;
            Near = MapGeometryProjection.Near / rules.Scale;
            Far = original.Far / rules.Scale;
            if (ceiling.HasValue)
            {
                // Clamp below the lowest bound to an empty depth interval within the
                // existing far volume: even an all-clipped scene receives clear paints.
                var cut = Math.Max(lowest - 10, Math.Min(highest + 9, ceiling.Value));
                Near = Math.Max(Near, (original.CameraZ - cut) / rules.Scale);
            }
            if (!MapLiveGeometryRules.Finite(Eye.X) || !MapLiveGeometryRules.Finite(Eye.Y) || !MapLiveGeometryRules.Finite(Eye.Z) ||
                !MapLiveGeometryRules.Finite(Eye.Z - 1) || Eye.Z - 1 == Eye.Z ||
                !MapLiveGeometryRules.Finite(Right) || Right <= 0 || !MapLiveGeometryRules.Finite(Bottom) || Bottom >= 0 ||
                !MapLiveGeometryRules.Finite(Near) || Near <= 0 || !MapLiveGeometryRules.Finite(Far) || Near >= Far)
                throw new ArgumentException("Invalid normalized runtime projection");
        }
    }
}
