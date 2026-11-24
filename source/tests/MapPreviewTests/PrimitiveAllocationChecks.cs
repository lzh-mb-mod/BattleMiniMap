using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class PrimitiveAllocationChecks
{
    static int checks;
    static ulong consumed;
    static void Check(bool value,string message){if(!value)throw new Exception(message);++checks;Console.WriteLine("PASS primitive allocation: "+message);}
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static void Consume(GeometryPoint a,GeometryPoint b,float radius)
    {
        foreach(var face in MapLivePrimitiveGeometry.Capsule(a,b,radius))
            for(var i=0;i<3;++i)unchecked{consumed=consumed*31+(uint)BitConverter.SingleToInt32Bits(face[i].X);}
    }
    static void Finish(MapLiveSceneGeometry live){for(var i=0;i<10000&&!live.InitialFinished&&live.Failure==null;++i)live.Update(.01f);}
    public static void Run(string reportPath)
    {
        var cases=new[]{
            new {name="sphere",a=new GeometryPoint(0,0,0),b=new GeometryPoint(0,0,0),r=2f},
            new {name="vertical",a=new GeometryPoint(0,0,0),b=new GeometryPoint(0,0,2),r=.5f},
            new {name="diagonal",a=new GeometryPoint(-3,5,2),b=new GeometryPoint(7,-11,9),r=4f},
            new {name="horizontal",a=new GeometryPoint(2,1,5),b=new GeometryPoint(-8,1,5),r=1f},
            new {name="near-axis",a=new GeometryPoint(0,0,0),b=new GeometryPoint(0,0,1e-7f),r=.1f},
            new {name="tiny",a=new GeometryPoint(0,0,0),b=new GeometryPoint(0,0,1),r=.00001f},
            new {name="large",a=new GeometryPoint(-100,200,0),b=new GeometryPoint(200,-500,300),r=100000f}};
        var output=new List<object>();
        foreach(var c in cases)
        {
            var faces=MapLivePrimitiveGeometry.Capsule(c.a,c.b,c.r).ToArray();
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {
                foreach(var f in faces)for(var i=0;i<3;++i){writer.Write(f[i].X);writer.Write(f[i].Y);writer.Write(f[i].Z);}
                var hash=Hash(stream.ToArray());
                Check(faces.Length>0&&faces.Length<=216,"Bounded nonempty primitive "+c.name);
                Consume(c.a,c.b,c.r);var bytes=new long[3];
                for(var run=0;run<3;++run)
                {var before=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<64;++i)Consume(c.a,c.b,c.r);bytes[run]=GC.GetAllocatedBytesForCurrentThread()-before;}
                output.Add(new{name=c.name,faces=faces.Length,orderedFloatBitsSha256=hash,repeats=64,allocatedBytes=bytes});
            }
        }
        foreach(var radius in new[]{0f,-1f,float.NaN,100001f})
        {
            var rejected=false;try{Consume(new GeometryPoint(),new GeometryPoint(),radius);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid radius rejected "+radius);
        }
        // Two suspended iterators must retain their own coordinates and buffer.
        using(var first=MapLivePrimitiveGeometry.Capsule(cases[0].a,cases[0].b,cases[0].r).GetEnumerator())
        using(var second=MapLivePrimitiveGeometry.Capsule(cases[2].a,cases[2].b,cases[2].r).GetEnumerator())
        {
            first.MoveNext();var x=first.Current[0].X;var y=first.Current[0].Y;
            while(second.MoveNext()){}
            Check(first.Current[0].X==x&&first.Current[0].Y==y,"Suspended iterator is not overwritten by another primitive");
        }
        var collectors=new List<object>();
        for(var run=0;run<3;++run)
        {
            var source=new Scene();var target=new Scene();var shapes=new List<PhysicsShape>();
            for(var i=0;i<58;++i)
            {
                var shape=new PhysicsShape{SphereN=i%2,CapsuleN=1-i%2};shapes.Add(shape);
                GameEntity.CreateEmpty(source,false,false).Shape=shape;GameEntity.CreateEmpty(source,false,false).Shape=shape;
            }
            var material=new Material();var rules=new MapLiveGeometryRules(0,0,8,8);
            var before=GC.GetAllocatedBytesForCurrentThread();var live=new MapLiveSceneGeometry(source,target,material,rules,false,true);
            Finish(live);var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(live.InitialFinished&&!live.Partial&&shapes.All(s=>s.Copies==1&&s.Reads==1),"58 shared mixed resources finish without changed preparation or coverage "+run);
            var signature=Hash(Encoding.UTF8.GetBytes(string.Join(";",target.Entities.Select(e=>e.Added.UploadSignature+":"+e.Added.UploadFaces+":"+e.Added.UploadVertices))));
            collectors.Add(new{run,allocatedBytes=bytes,instances=target.Entities.Count,orderedUploadSha256=signature});live.Dispose();
        }
        if(reportPath!=null)File.WriteAllText(reportPath,JsonSerializer.Serialize(new{scope="net9 own thread; standalone64 repetitions and 58 shared mixed resources collector; fixtures/teardown excluded; fake engine; no game speed claim",primitives=output,collectors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS: "+checks+" primitive equivalence/allocation fixture checks; ordered bits require old/new report comparison");
    }
}
