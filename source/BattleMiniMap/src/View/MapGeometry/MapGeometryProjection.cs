using System;

namespace BattleMiniMap.View.MapGeometry
{
    internal sealed class MapGeometryProjection
    {
        private readonly float _minX, _minY;
        public float Right { get; }
        public float Bottom { get; }
        public float CameraZ { get; }
        public float Far { get; }
        public const float Near = .1f;
        public MapGeometryProjection(float minX, float minY, float resolution, int width, int height, float lowest, float highest)
        {
            if (!(resolution > 0) || float.IsInfinity(resolution) || width <= 0 || height <= 0 ||
                float.IsNaN(minX) || float.IsNaN(minY) || float.IsInfinity(minX) || float.IsInfinity(minY) ||
                float.IsNaN(lowest) || float.IsNaN(highest) || float.IsInfinity(lowest) || float.IsInfinity(highest) || highest < lowest)
                throw new ArgumentOutOfRangeException(nameof(resolution));
            _minX = minX; _minY = minY;
            Right = width * resolution; Bottom = -height * resolution;
            if (float.IsInfinity(Right) || float.IsInfinity(Bottom)) throw new ArgumentOutOfRangeException(nameof(resolution));
            CameraZ = highest + 10; Far = highest - lowest + 30;
            if (float.IsInfinity(CameraZ) || float.IsInfinity(Far)) throw new ArgumentOutOfRangeException(nameof(highest));
        }
        public GeometryPoint TextureUv(GeometryPoint world) => new GeometryPoint(
            (world.Y - _minY) / Right, (world.X - _minX) / -Bottom,
            (CameraZ - world.Z - Near) / (Far - Near));
    }
}
