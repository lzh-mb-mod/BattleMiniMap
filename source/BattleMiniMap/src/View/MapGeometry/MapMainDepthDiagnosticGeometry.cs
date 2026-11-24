using System;

namespace BattleMiniMap.View.MapGeometry
{
    // Three opaque quads in the map's world/UV domain, independent of source meshes.
    internal static class MapMainDepthDiagnosticGeometry
    {
        internal sealed class Plane
        {
            public readonly string Name;
            public readonly uint Color;
            public readonly GeometryPoint[] Corners;
            public Plane(string name, uint color, GeometryPoint[] corners) { Name=name; Color=color; Corners=corners; }
        }
        public static Plane[] Create(float minX, float minY, float resolution, int width, int height,
            float lowest, float highest, bool reverse)
        {
            var span=(double)highest-lowest;
            if (!MapLiveGeometryRules.Finite(lowest) || !MapLiveGeometryRules.Finite(highest) || span<=0)
                throw new ArgumentException("Depth probe requires a positive public height interval");
            var mid=(float)(lowest+span*.5); var gap=(float)(span*.125);
            var planes=new[] {
                Quad("ground-gray",0xff808080u,.25f,.75f,.25f,.75f,mid,minX,minY,resolution,width,height),
                Quad("high-yellow",0xffffff00u,.50f,.90f,.45f,.65f,mid+gap,minX,minY,resolution,width,height),
                Quad("low-blue",0xff0070ffu,.10f,.50f,.35f,.55f,mid-gap,minX,minY,resolution,width,height)
            };
            if (!(planes[2].Corners[0].Z<mid && mid<planes[1].Corners[0].Z))
                throw new ArgumentException("Depth probe heights are not distinct");
            if(reverse)Array.Reverse(planes);
            return planes;
        }
        private static Plane Quad(string name,uint color,float minU,float maxU,float minV,float maxV,float z,
            float minX,float minY,float resolution,int width,int height)
        {
            var points=new[] {
                new GeometryPoint(minX+minV*resolution*height,minY+minU*resolution*width,z),
                new GeometryPoint(minX+minV*resolution*height,minY+maxU*resolution*width,z),
                new GeometryPoint(minX+maxV*resolution*height,minY+maxU*resolution*width,z),
                new GeometryPoint(minX+maxV*resolution*height,minY+minU*resolution*width,z)
            };
            foreach(var p in points)
                if(!MapLiveGeometryRules.Finite(p.X)||!MapLiveGeometryRules.Finite(p.Y)||!MapLiveGeometryRules.Finite(p.Z))
                    throw new ArgumentException("Invalid depth probe world coordinates");
            return new Plane(name,color,points);
        }
    }
}
