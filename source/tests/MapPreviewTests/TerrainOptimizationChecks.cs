using System;
using System.Linq;
using System.Collections.Generic;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;
using TaleWorlds.Library;

static class TerrainOptimizationChecks
{
    static int checks;
    static void Check(bool value,string text){if(!value)throw new Exception(text);++checks;Console.WriteLine("PASS terrain optimization: "+text);}
    static MapTerrainHeightfield Field(int side,Func<int,int,double> z,bool hole=false)
    {
        var b=new byte[side*side*4];
        for(var y=0;y<side;++y)for(var x=0;x<side;++x)
        {
            var index=(y*side+x)*4;var n=(short)Math.Round(z(x,y)*1000);
            b[index]=(byte)n;b[index+1]=(byte)(n>>8);b[index+2]=(byte)(((x+y)%2)*128);
        }
        if(hole)b[2]=255;
        return new MapTerrainHeightfield(side,side,b,0,0,1,.001f,0);
    }
    static MapTerrainPatch Patch(MapTerrainHeightfield f,int x=0,int y=0,int w=8,int h=8,bool enable=true)
    {var patch=new MapTerrainPatch(f,x,y,w,h);foreach(var step in patch.Evaluate(enable)){}return patch;}
    static double Height(MapTerrainHeightfield f,IEnumerable<MapTerrainFace> faces,double x,double y)
    {
        foreach(var indices in faces)
        {
            var a=f.Vertex(indices[0]);var b=f.Vertex(indices[1]);var c=f.Vertex(indices[2]);
            var det=(b.Y-c.Y)*(a.X-c.X)+(c.X-b.X)*(a.Y-c.Y);
            var u=((b.Y-c.Y)*(x-c.X)+(c.X-b.X)*(y-c.Y))/det;
            var v=((c.Y-a.Y)*(x-c.X)+(a.X-c.X)*(y-c.Y))/det;var w=1-u-v;
            if(u>=-1e-8&&v>=-1e-8&&w>=-1e-8)return u*a.Z+v*b.Z+w*c.Z;
        }
        throw new Exception("Uncovered triangle point");
    }
    static void Finish(MapLiveSceneGeometry c){for(var i=0;i<1000&&!c.InitialFinished&&c.Failure==null;++i)c.Update(.01f);}
    // Optional existing CSV inputs only. No game connection or new capture is performed.
    // args: cache.xml report.json columns.csv [columns.csv ...]
    public static void ExistingSamples(string[] args)
    {
        var report=new List<object>();
        foreach(var path in args.Skip(2))
        {
            var bytes=System.IO.File.ReadAllBytes(path);
            var rows=new List<Dictionary<string,string>>();
            using(var parser=new Microsoft.VisualBasic.FileIO.TextFieldParser(path))
            {
                parser.SetDelimiters(",");parser.HasFieldsEnclosedInQuotes=true;var names=parser.ReadFields();
                while(!parser.EndOfData){var values=parser.ReadFields();rows.Add(names.Select((n,i)=>new {n,v=values[i]}).ToDictionary(a=>a.n,a=>a.v));}
            }
            double Number(string v)=>double.Parse(v,System.Globalization.CultureInfo.InvariantCulture);
            var xs=rows.Select(r=>Number(r["x"])).Distinct().OrderBy(v=>v).ToArray();
            var ys=rows.Select(r=>Number(r["y"])).Distinct().OrderBy(v=>v).ToArray();
            if(xs.Length<2||ys.Length<2||rows.Count!=xs.Length*ys.Length)throw new Exception("Incomplete sampled rectangle: "+path);
            var step=xs[1]-xs[0];
            if(xs.Where((v,i)=>Math.Abs(v-xs[0]-i*step)>.001).Any()||ys.Where((v,i)=>Math.Abs(v-ys[0]-i*step)>.001).Any())throw new Exception("Nonuniform sampled grid");
            var input=new double[rows.Count];var seen=new HashSet<int>();
            foreach(var r in rows)
            {
                var x=Array.BinarySearch(xs,Number(r["x"]));var y=Array.BinarySearch(ys,Number(r["y"]));var value=Number(r["terrain_height"]);
                if(!double.IsFinite(value)||!seen.Add(y*xs.Length+x))throw new Exception("Invalid or duplicate height");input[y*xs.Length+x]=value;
            }
            var min=input.Min();var max=input.Max();var offset=(float)((min+max)/2);var scale=(float)Math.Max(.0001,(max-min)/65000);
            foreach(var diagonal in new[]{0,128})
            {
                var data=new byte[input.Length*4];double quantization=0;
                for(var i=0;i<input.Length;++i)
                {
                    var n=checked((short)Math.Round((input[i]-offset)/scale));data[i*4]=(byte)n;data[i*4+1]=(byte)(n>>8);data[i*4+2]=(byte)diagonal;
                    quantization=Math.Max(quantization,Math.Abs(input[i]-((float)n*scale+offset)));
                }
                var f=new MapTerrainHeightfield(ys.Length,xs.Length,data,(float)xs[0],(float)ys[0],(float)step,scale,offset);
                var watch=System.Diagnostics.Stopwatch.StartNew();long faces=0,algorithmTicks=0,algorithmAllocated=0;var accepted=0;var exact=0;double maxAccepted=0,maxSampleError=0;
                var topology=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                for(var y=0;y<ys.Length-1;y+=8)for(var x=0;x<xs.Length-1;x+=8)
                {
                    var w=Math.Min(8,xs.Length-1-x);var h=Math.Min(8,ys.Length-1-y);var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();var algorithmStart=System.Diagnostics.Stopwatch.GetTimestamp();var selected=new List<MapTerrainPatch>();foreach(var tick in MapTerrainPatch.Select(f,x,y,w,h,true,p=>selected.Add(p))){}
                    var output=selected.SelectMany(p=>p.Faces()).ToArray();algorithmTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-algorithmStart;algorithmAllocated+=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;faces+=output.Length;
                    foreach(var face in output){topology.AppendData(BitConverter.GetBytes(face.A));topology.AppendData(BitConverter.GetBytes(face.B));topology.AppendData(BitConverter.GetBytes(face.C));}
                    foreach(var patch in selected){if(patch.Simplified){++accepted;maxAccepted=Math.Max(maxAccepted,patch.Error);}else ++exact;}
                    // Compare against original recorded heights, including S16 encoding error.
                    for(var py=0;py<=h;++py)for(var px=0;px<=w;++px)
                    {
                        var point=f.Vertex((y+py)*xs.Length+x+px);
                        maxSampleError=Math.Max(maxSampleError,Math.Abs(Height(f,output,point.X,point.Y)-input[(y+py)*xs.Length+x+px]));
                    }
                }
                if(maxSampleError>.10001+quantization)throw new Exception("Existing sample exceeded error bound");
                var sourceFaces=2L*(xs.Length-1)*(ys.Length-1);
                report.Add(new {path,bytes=bytes.Length,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),columns=xs.Length,rows=ys.Length,step,diagonalAssumption=diagonal,holes="not recorded; no-hole reconstructed test surface",sourceFaces,displayFaces=faces,accepted,exact,maxAccepted,maxSampleError,quantization,offlineValidationMs=watch.Elapsed.TotalMilliseconds,simplificationMs=algorithmTicks*1000d/System.Diagnostics.Stopwatch.Frequency,simplificationAllocatedBytes=algorithmAllocated,topologySha256=Convert.ToHexString(topology.GetHashAndReset()).ToLowerInvariant(),uploadBlocks=((xs.Length-2)/64+1)*((ys.Length-2)/64+1)});
                topology.Dispose();
                Console.WriteLine("EXISTING HEIGHTS "+System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path))+" diagonal="+diagonal+" faces="+sourceFaces+" -> "+faces+" maxSampleError="+maxSampleError.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        System.IO.File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Run()
    {
        var f=Field(9,(x,y)=>x*.03+y*.05);var patch=Patch(f);
        Check(patch.Simplified&&patch.Faces().Count()==32&&patch.Error<1e-5,"Planar patch reduces 128 source faces to 32");
        Check(Patch(f,enable:false).Faces().Count()==128,"Full-topology mode retains every original face");
        f=Field(9,(x,y)=>x==3&&y==3?2:0);patch=Patch(f);
        Check(!patch.Simplified&&patch.Faces().Count()==128,"Interior spike exceeds tolerance and keeps exact topology");
        f=Field(9,(x,y)=>0,true);patch=Patch(f);
        Check(!patch.Simplified&&patch.HasHoles&&patch.Faces().Count()==127,"Half-cell hole forces exact patch and never fills missing triangle");
        f=Field(9,(x,y)=>0);patch=Patch(f,w:7);
        Check(!patch.Simplified&&patch.Faces().Count()==112,"Odd remainder patch uses exact source topology");
        f=Field(17,(x,y)=>x<8?0:(x==11&&y==3?3:0));var left=Patch(f);var right=Patch(f,8);
        var leftEdge=new HashSet<int>(left.Faces().SelectMany(a=>new[]{a.A,a.B,a.C}).Where(i=>i%17==8));
        var rightEdge=new HashSet<int>(right.Faces().SelectMany(a=>new[]{a.A,a.B,a.C}).Where(i=>i%17==8));
        Check(left.Simplified&&!right.Simplified&&leftEdge.SetEquals(rightEdge)&&leftEdge.Count==9,"Mixed exact/simplified patches retain identical complete shared border");
        f=Field(9,(x,y)=>x==3&&y==3?2:0);var selected=new List<MapTerrainPatch>();
        foreach(var tick in MapTerrainPatch.Select(f,0,0,8,8,true,p=>selected.Add(p))){}
        var adaptiveFaces=selected.SelectMany(p=>p.Faces()).ToArray();var exactFaces=Patch(f,enable:false).Faces().ToArray();
        Check(selected.Count==4&&adaptiveFaces.Length==80,"Rejected 8-cell patch splits into three simplified and one exact 4-cell patch");
        double adaptiveError=0;for(var y=0;y<=32;++y)for(var x=0;x<=32;++x)adaptiveError=Math.Max(adaptiveError,Math.Abs(Height(f,adaptiveFaces,x/4d,y/4d)-Height(f,exactFaces,x/4d,y/4d)));
        Check(adaptiveError<1e-6,"Adaptive exact/simplified neighbours preserve the spike and full shared edges");
        var random=new Random(812);var accepted=0;var rejected=0;
        for(var sample=0;sample<12;++sample)
        {
            var amplitude=sample<6?.025:.25;
            f=Field(9,(x,y)=>(random.NextDouble()-.5)*amplitude+x*.01);
            patch=Patch(f);if(patch.Simplified)++accepted;else ++rejected;
            var original=Patch(f,enable:false).Faces().ToArray();var displayed=patch.Faces().ToArray();
            double max=0;
            for(var y=0;y<=32;++y)for(var x=0;x<=32;++x)
                max=Math.Max(max,Math.Abs(Height(f,original,x/4d,y/4d)-Height(f,displayed,x/4d,y/4d)));
            Check(max<=.100001,"Independent dense barycentric comparison respects error bound, seed case "+sample);
        }
        Check(accepted>0&&rejected>0,"Random fixtures exercise both acceptance and exact fallback");
        // A larger smooth field exercises multiple neighbouring patches, without game reads.
        var watch=System.Diagnostics.Stopwatch.StartNew();f=Field(129,(x,y)=>x*.01+y*.01);
        long faces=0;for(var y=0;y<128;y+=8)for(var x=0;x<128;x+=8)faces+=Patch(f,x,y).Faces().Count();
        Check(faces==8192,"129-square planar node reduces 32768 faces to 8192 with four 64-cell uploads");
        Console.WriteLine("Synthetic 129-square simplification ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        foreach(var kind in new[]{1,2})
        {
            var source=new Scene();var e=GameEntity.CreateEmpty(source,false,false);e.Shape=new PhysicsShape{TriangleKind=kind};
            var shared=GameEntity.CreateEmpty(source,false,false);shared.Shape=e.Shape;
            var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,true);Finish(live);
            var reason=kind==1?"triangle_degenerate":"triangle_nonfinite";
            Check(live.Errors==1&&live.ErrorCount(reason)==1&&live.CopiedMeshes==0,"Shared shape reports one resource-level "+reason);live.Dispose();
        }
        foreach(var radius in new[]{float.NaN,0f,100001f})
        {
            var source=new Scene();var e=GameEntity.CreateEmpty(source,false,false);e.Shape=new PhysicsShape{MeshN=0,SphereN=1,TestRadius=radius};
            var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,true);Finish(live);
            var reason=float.IsNaN(radius)?"primitive_radius_nonfinite":radius==0?"primitive_radius_nonpositive":"primitive_radius_out_of_range";
            Check(live.Errors==1&&live.ErrorCount(reason)==1,"Primitive rejects separately: "+reason);live.Dispose();
        }
        foreach(var origin in new[]{true,false})
        {
            var source=new Scene();var e=GameEntity.CreateEmpty(source,false,false);e.Shape=new PhysicsShape();
            if(origin)e.Frame.origin.x=float.NaN;else e.Frame.rotation.s.x=float.NaN;
            var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,true);Finish(live);
            Check(live.ErrorCount(origin?"body_origin_nonfinite":"body_axes_nonfinite")==1,"Body transform origin and axes errors distinguished");live.Dispose();
        }
        var endpointSource=new Scene();GameEntity.CreateEmpty(endpointSource,false,false).Shape=new PhysicsShape{MeshN=0,SphereN=1,TestOrigin=new Vec3(float.NaN,0,0)};
        var endpointCollector=new MapLiveSceneGeometry(endpointSource,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,true);Finish(endpointCollector);
        Check(endpointCollector.ErrorCount("primitive_endpoint_nonfinite")==1,"Nonfinite primitive endpoints distinguished from radius errors");endpointCollector.Dispose();
        var scene=new Scene();for(var i=0;i<20;++i)GameEntity.CreateEmpty(scene,false,false).Shape=new PhysicsShape{TriangleKind=1};
        var begin=Debug.Messages.Count;
        var collector=new MapLiveSceneGeometry(scene,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,true);Finish(collector);
        Check(collector.Errors==20&&Debug.Messages.Skip(begin).Count(s=>s.Contains("object rejected:"))==4&&Debug.Messages.Skip(begin).Count(s=>s.Contains("collision resource errors:"))==20,"Repeated reasons keep complete counts but cap example logging");collector.Dispose();
        Console.WriteLine("PASS: "+checks+" terrain optimization and collision diagnostics checks; synthetic fields, no game sampling");
    }
}
