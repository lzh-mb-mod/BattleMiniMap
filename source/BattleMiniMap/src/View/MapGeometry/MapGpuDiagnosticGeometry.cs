using System;
using System.Linq;
namespace BattleMiniMap.View.MapGeometry
{
    internal sealed class MapGpuDiagnosticGeometry
    {
        public readonly GeometryPoint Center;
        public readonly float Scale;
        public MapGpuDiagnosticGeometry(MapGeometryData data)
        {
            var vertices = data.Layers.SelectMany(l => l.Original.Vertices).ToArray();
            var minX = vertices.Min(p => p.X); var maxX = vertices.Max(p => p.X);
            var minY = vertices.Min(p => p.Y); var maxY = vertices.Max(p => p.Y);
            var minZ = vertices.Min(p => p.Z); var maxZ = vertices.Max(p => p.Z);
            Center = new GeometryPoint((minX+maxX)/2,(minY+maxY)/2,(minZ+maxZ)/2);
            Scale = Math.Max(maxX-minX,Math.Max(maxY-minY,maxZ-minZ))/2;
            if (Scale <= 0 || float.IsNaN(Scale) || float.IsInfinity(Scale)) throw new ArgumentException("Invalid diagnostic bounds");
        }
        public GeometryPoint Normalize(GeometryPoint p) => new GeometryPoint((p.Y-Center.Y)/Scale,-(p.X-Center.X)/Scale,(p.Z-Center.Z)/Scale);
        public static GeometryPoint ExpectedViewport(GeometryPoint p) => new GeometryPoint((p.X+1.25f)/2.5f,(1.25f-p.Y)/2.5f,400-p.Z);
        public static GeometryPoint[] Triangle => new[]{new GeometryPoint(-.8f,-.7f,0),new GeometryPoint(.8f,-.7f,0),new GeometryPoint(0,.8f,0)};
    }
}
