using System;

namespace BattleMiniMap.View.MapGeometry
{
    internal sealed class MapLiveGeometryRules
    {
        public readonly float MinX, MinY, MaxX, MaxY, Scale;
        public readonly int GridX, GridY;
        public MapLiveGeometryRules(float minX,float minY,float maxX,float maxY)
        {
            if (!Finite(minX)||!Finite(minY)||!Finite(maxX)||!Finite(maxY)||maxX<=minX||maxY<=minY || maxX-minX>100000 || maxY-minY>100000)
                throw new ArgumentException("Invalid runtime map domain");
            MinX=minX; MinY=minY; MaxX=maxX; MaxY=maxY;
            Scale=Math.Max(maxX-minX,maxY-minY)/2;
            if (Scale<.001f || !Finite(Scale)) throw new ArgumentException("Runtime map domain is too small");
            GridX=Math.Max(2,Math.Min(257,(int)Math.Ceiling((maxX-minX)/4)+1));
            GridY=Math.Max(2,Math.Min(257,(int)Math.Ceiling((maxY-minY)/4)+1));
        }
        public static bool Finite(float x) => !float.IsNaN(x)&&!float.IsInfinity(x);
        public GeometryPoint Normalize(GeometryPoint p) => new GeometryPoint((p.Y-(MinY+MaxY)/2)/Scale,-(p.X-(MinX+MaxX)/2)/Scale,p.Z/Scale);
        public float X(int index) => MinX+(MaxX-MinX)*index/(GridX-1);
        public float Y(int index) => MinY+(MaxY-MinY)*index/(GridY-1);
        // Mask bits describe the LOD membership of each source submesh.
        public static int CoarsestLod(int mask)
        {
            for(var i=15;i>=0;--i) if ((mask&(1<<i))!=0) return 1<<i;
            return 0;
        }
    }
}
