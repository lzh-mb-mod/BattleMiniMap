using System;
using System.Linq;

namespace BattleMiniMap.View.MapGeometry
{
    // Deterministic display fallback. Rasterizes cached triangles only; never reads the mission scene.
    internal static class MapGeometryRasterizer
    {
        internal sealed class Result
        {
            public byte[] Pixels;
            public int Width, Height, CoveredPixels, Triangles;
        }
        public static Result Render(MapGeometryData data, bool proxy, float? ceiling,
            float minX, float minY, float resolution, int mapWidth, int mapHeight, int width, int height)
        {
            if (width <= 0 || height <= 0 || width > 2048 || height > 2048 || resolution <= 0 ||
                float.IsNaN(resolution) || float.IsInfinity(resolution) || mapWidth <= 0 || mapHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(resolution));
            var result = new Result { Width = width, Height = height, Pixels = new byte[checked(width * height * 4)] };
            var depth = new float[width * height];
            for (var i = 0; i < depth.Length; ++i) depth[i] = float.NegativeInfinity;
            foreach (var layer in data.Layers)
            foreach (var triangle in MapGeometryData.Triangles(data.Select(layer, proxy), ceiling))
            {
                ++result.Triangles;
                var points = triangle.Select(p => new GeometryPoint(
                    (p.Y - minY) / (resolution * mapWidth) * width,
                    (p.X - minX) / (resolution * mapHeight) * height, p.Z)).ToArray();
                var a = points[0]; var b = points[1]; var c = points[2];
                var area = Edge(a, b, c.X, c.Y);
                if (Math.Abs(area) < 1e-10) continue; // Vertical face is edge-on in a top-down map.
                var loX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
                var hiX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
                var loY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
                var hiY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
                var main = layer.Resource == "stone_bridge_wide";
                for (var y = loY; y <= hiY; ++y)
                for (var x = loX; x <= hiX; ++x)
                {
                    var u = Edge(b, c, x + .5f, y + .5f) / area;
                    var v = Edge(c, a, x + .5f, y + .5f) / area;
                    var w = 1 - u - v;
                    if (u < -1e-6 || v < -1e-6 || w < -1e-6) continue;
                    var z = u * a.Z + v * b.Z + w * c.Z; var index = y * width + x;
                    if (z <= depth[index]) continue;
                    if (float.IsNegativeInfinity(depth[index])) ++result.CoveredPixels;
                    depth[index] = (float)z; var pixel = index * 4;
                    result.Pixels[pixel] = main ? (byte)206 : (byte)130;
                    result.Pixels[pixel + 1] = main ? (byte)184 : (byte)166;
                    result.Pixels[pixel + 2] = main ? (byte)143 : (byte)183;
                    result.Pixels[pixel + 3] = 255;
                }
            }
            return result;
        }
        private static double Edge(GeometryPoint a, GeometryPoint b, float x, float y) =>
            (double)(b.X - a.X) * (y - a.Y) - (double)(b.Y - a.Y) * (x - a.X);
    }
}
