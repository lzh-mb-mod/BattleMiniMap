using System;
using System.Collections.Generic;

namespace BattleMiniMap.View.MapGeometry
{
    internal static class MapLivePrimitiveGeometry
    {
        // Display approximation only: 12 segments, four rings per hemisphere.
        public static IEnumerable<GeometryPoint[]> Capsule(GeometryPoint a,GeometryPoint b,float radius)
        {
            if (!Valid(a)||!Valid(b)||!MapLiveGeometryRules.Finite(radius)||radius<=0||radius>100000)
                throw new ArgumentException("Invalid collision primitive");
            var axis=Sub(b,a); var length=Length(axis);
            axis=length>1e-6 ? Mul(axis,1/length) : new GeometryPoint(0,0,1);
            var reference=Math.Abs(axis.Z)<.9 ? new GeometryPoint(0,0,1) : new GeometryPoint(0,1,0);
            var side=Cross(axis,reference);side=Mul(side,1/Length(side));var up=Cross(axis,side);
            var rings=new List<GeometryPoint[]>();
            for(var half=0;half<2;++half) for(var k=0;k<=4;++k)
            {
                var angle=half==0 ? -Math.PI/2+k*Math.PI/8 : k*Math.PI/8;
                var center=half==0?a:b;var ring=new GeometryPoint[12];
                for(var j=0;j<12;++j)
                {
                    var t=j*2*Math.PI/12;
                    ring[j]=Add(center,Add(Mul(axis,(float)(radius*Math.Sin(angle))),
                        Add(Mul(side,(float)(radius*Math.Cos(angle)*Math.Cos(t))),Mul(up,(float)(radius*Math.Cos(angle)*Math.Sin(t))))));
                }
                rings.Add(ring);
            }
            for(var k=0;k<rings.Count-1;++k) for(var j=0;j<12;++j)
            {
                var n=(j+1)%12;var p=rings[k][j];var q=rings[k][n];var r=rings[k+1][j];var s=rings[k+1][n];
                if(Length(Cross(Sub(q,p),Sub(r,p)))>1e-10) yield return new[]{p,q,r};
                if(Length(Cross(Sub(s,q),Sub(r,q)))>1e-10) yield return new[]{q,s,r};
            }
        }
        private static bool Valid(GeometryPoint p)=>MapLiveGeometryRules.Finite(p.X)&&MapLiveGeometryRules.Finite(p.Y)&&MapLiveGeometryRules.Finite(p.Z);
        private static GeometryPoint Add(GeometryPoint a,GeometryPoint b)=>new GeometryPoint(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        private static GeometryPoint Sub(GeometryPoint a,GeometryPoint b)=>new GeometryPoint(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        private static GeometryPoint Mul(GeometryPoint a,float n)=>new GeometryPoint(a.X*n,a.Y*n,a.Z*n);
        private static float Length(GeometryPoint p)=>(float)Math.Sqrt((double)p.X*p.X+(double)p.Y*p.Y+(double)p.Z*p.Z);
        private static GeometryPoint Cross(GeometryPoint a,GeometryPoint b)=>new GeometryPoint(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    }
}
