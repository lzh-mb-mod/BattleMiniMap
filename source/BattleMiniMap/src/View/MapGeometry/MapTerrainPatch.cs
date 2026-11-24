using System;
using System.Collections.Generic;

namespace BattleMiniMap.View.MapGeometry
{
    internal readonly struct MapTerrainFace
    {
        public readonly int A,B,C;
        public MapTerrainFace(int a,int b,int c){A=a;B=b;C=c;}
        public int this[int k]=>k==0?A:k==1?B:C;
    }
    // All indices refer to original samples. Patch borders are NEVER decimated.
    internal sealed class MapTerrainPatch
    {
        private readonly MapTerrainHeightfield _tile;
        private readonly int _x,_y,_w,_h;
        private double _error;
        private float[] _heights;
        private ulong _readyLow,_readyHigh;
        private Plan _plan;
        private sealed class Plan
        {
            public readonly Tuple<int,int>[] Border;
            public readonly double[][] Cuts;
            public Plan(int w,int h)
            {
                var border=new List<Tuple<int,int>>();
                for(var x=0;x<w;++x)border.Add(Tuple.Create(x,0));
                for(var y=0;y<h;++y)border.Add(Tuple.Create(w,y));
                for(var x=w;x>0;--x)border.Add(Tuple.Create(x,h));
                for(var y=h;y>0;--y)border.Add(Tuple.Create(0,y));
                Border=border.ToArray();Cuts=new double[Border.Length][];
                for(var edge=0;edge<Border.Length;++edge)
                {
                    var dx=Border[edge].Item1-w/2d;var dy=Border[edge].Item2-h/2d;var cuts=new List<double>{0,1};
                    if(dx!=0)for(var x=1;x<w;++x){var t=(x-w/2d)/dx;if(t>0&&t<1)cuts.Add(t);}
                    if(dy!=0)for(var y=1;y<h;++y){var t=(y-h/2d)/dy;if(t>0&&t<1)cuts.Add(t);}
                    cuts.Sort();Cuts[edge]=cuts.ToArray();
                }
            }
        }
        private static readonly Lazy<Plan>[,] Plans=CreatePlans();
        private static Lazy<Plan>[,] CreatePlans()
        {
            var plans=new Lazy<Plan>[3,3];
            for(var w=4;w<=8;w+=2)for(var h=4;h<=8;h+=2)
            {var width=w;var height=h;plans[(w-4)/2,(h-4)/2]=new Lazy<Plan>(()=>new Plan(width,height));}
            return plans;
        }
        private Plan GeometryPlan=>_plan??(_plan=Plans[(_w-4)/2,(_h-4)/2].Value);
        public bool Simplified { get; private set; }
        public bool HasHoles { get; private set; }
        public double Error => _error;
        public MapTerrainPatch(MapTerrainHeightfield tile,int x,int y,int w,int h)
        { _tile=tile;_x=x;_y=y;_w=w;_h=h; }
        private int Index(int x,int y)=>(_y+y)*_tile.Columns+_x+x;
        private double Z(int x,int y)
        {
            var index=y*(_w+1)+x;var bit=1UL<<(index&63);
            if(_heights==null)_heights=new float[(_w+1)*(_h+1)];
            if(index<64)
            {if((_readyLow&bit)==0){_heights[index]=_tile.Height(Index(x,y));_readyLow|=bit;}}
            else if((_readyHigh&bit)==0){_heights[index]=_tile.Height(Index(x,y));_readyHigh|=bit;}
            return _heights[index];
        }
        private bool Measure(double a,double b,double tolerance)
        {
            var d=Math.Abs(a-b);
            if(double.IsNaN(d)||double.IsInfinity(d))return false;
            _error=Math.Max(_error,d);return d<=tolerance;
        }
        // Interpolate a candidate fan directly by intersecting the centre ray with its border.
        private double Fan(double x,double y)
        {
            var cx=_w/2d;var cy=_h/2d;var dx=x-cx;var dy=y-cy;
            var t=Math.Max(Math.Abs(dx)/cx,Math.Abs(dy)/cy);
            if(t<1e-14)return Z(_w/2,_h/2);
            var bx=Math.Max(0,Math.Min(_w,cx+dx/t));var by=Math.Max(0,Math.Min(_h,cy+dy/t));
            double edge;
            if(Math.Abs(dx)/cx>=Math.Abs(dy)/cy)
            {
                var ix=dx>0?_w:0;var iy=Math.Min(_h-1,(int)Math.Floor(by));
                edge=Z(ix,iy)*(1-(by-iy))+Z(ix,iy+1)*(by-iy);
            }
            else
            {
                var iy=dy>0?_h:0;var ix=Math.Min(_w-1,(int)Math.Floor(bx));
                edge=Z(ix,iy)*(1-(bx-ix))+Z(ix+1,iy)*(bx-ix);
            }
            return Z(_w/2,_h/2)*(1-t)+edge*t;
        }
        private double Original(double x,double y)
        {
            var ix=Math.Max(0,Math.Min(_w-1,(int)Math.Floor(x)));
            var iy=Math.Max(0,Math.Min(_h-1,(int)Math.Floor(y)));
            var fx=x-ix;var fy=y-iy;
            var z00=Z(ix,iy);var z10=Z(ix+1,iy);var z01=Z(ix,iy+1);var z11=Z(ix+1,iy+1);
            if(_tile.AlternateDiagonal(_y+iy,_x+ix))
                return fx>=fy ? z00*(1-fx)+z10*(fx-fy)+z11*fy : z00*(1-fy)+z01*(fy-fx)+z11*fx;
            return fx+fy<=1 ? z00*(1-fx-fy)+z10*fx+z01*fy : z11*(fx+fy-1)+z01*(1-fx)+z10*(1-fy);
        }
        // Overlay vertices are source vertices plus fan radial intersections with source edges.
        // Work is split at each cell/edge; the collector controls the frame budget.
        public IEnumerable<int> Evaluate(bool enable,double tolerance=.10)
        {
            var work=0;
            if(!enable||_w<4||_h<4||_w>8||_h>8||(_w&1)!=0||(_h&1)!=0)yield break;
            for(var y=0;y<_h;++y)for(var x=0;x<_w;++x)
            {
                if(_tile.Hole(_y+y,_x+x,0)||_tile.Hole(_y+y,_x+x,1)){HasHoles=true;yield break;}
                if(++work%32==0)yield return 0;
            }
            // Small double arithmetic allowance is subtracted, never added to the tolerance.
            var limit=Math.Max(0,tolerance-1e-7);
            for(var y=0;y<=_h;++y)for(var x=0;x<=_w;++x)
            {
                if(!Measure(Z(x,y),Fan(x,y),limit))yield break;
                if(++work%32==0)yield return 0;
            }
            var cx=_w/2d;var cy=_h/2d;var zc=Z(_w/2,_h/2);
            var plan=GeometryPlan;
            for(var edge=0;edge<plan.Border.Length;++edge)
            {
                var end=plan.Border[edge];
                var dx=end.Item1-cx;var dy=end.Item2-cy;var ze=Z(end.Item1,end.Item2);
                var cuts=plan.Cuts[edge];
                for(var i=0;i<cuts.Length;++i)
                {
                    var t=cuts[i];
                    if(!Measure(Original(cx+dx*t,cy+dy*t),zc*(1-t)+ze*t,limit))yield break;
                    if(i+1<cuts.Length&&cuts[i+1]-t>1e-12)
                    {
                        var next=cuts[i+1];var mid=(t+next)/2;
                        var ix=Math.Min(_w-1,Math.Max(0,(int)Math.Floor(cx+dx*mid)));
                        var iy=Math.Min(_h-1,Math.Max(0,(int)Math.Floor(cy+dy*mid)));
                        var alt=_tile.AlternateDiagonal(_y+iy,_x+ix);
                        var denominator=alt?dx-dy:dx+dy;
                        if(Math.Abs(denominator)>1e-14)
                        {
                            var crossing=alt?((ix-iy)-(cx-cy))/denominator:((ix+iy+1)-(cx+cy))/denominator;
                            if(crossing>t&&crossing<next && !Measure(Original(cx+dx*crossing,cy+dy*crossing),zc*(1-crossing)+ze*crossing,limit))yield break;
                        }
                    }
                    if(++work%32==0)yield return 0;
                }
            }
            Simplified=true;
        }
        public static IEnumerable<int> Select(MapTerrainHeightfield tile,int x,int y,int w,int h,bool enable,Action<MapTerrainPatch> complete)
        {
            var patch=new MapTerrainPatch(tile,x,y,w,h);
            foreach(var step in patch.Evaluate(enable))yield return step;
            // Preserve error bound on rough ground without forcing the whole 8-cell patch exact.
            if(enable&&!patch.Simplified&&(w>4||h>4))
            {
                var sw=w>4?4:w;var sh=h>4?4:h;
                for(var dy=0;dy<h;dy+=sh)for(var dx=0;dx<w;dx+=sw)
                    foreach(var step in Select(tile,x+dx,y+dy,Math.Min(sw,w-dx),Math.Min(sh,h-dy),enable,complete))yield return step;
            }
            else complete(patch);
        }
        public IEnumerable<MapTerrainFace> Faces()
        {
            if(Simplified)
            {
                var border=GeometryPlan.Border;var centre=Index(_w/2,_h/2);
                for(var i=0;i<border.Length;++i)
                {var a=border[i];var b=border[(i+1)%border.Length];yield return new MapTerrainFace(centre,Index(a.Item1,a.Item2),Index(b.Item1,b.Item2));}
            }
            else for(var y=0;y<_h;++y)for(var x=0;x<_w;++x)for(var half=0;half<2;++half)
                if(_tile.Triangle(_y+y,_x+x,half,out var a,out var b,out var c))yield return new MapTerrainFace(a,b,c);
        }
    }
}
