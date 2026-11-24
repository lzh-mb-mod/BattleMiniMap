using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class RawTerrainChecks
{
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);++checks;Console.WriteLine("PASS raw terrain: "+text);}
    static void Reject(Action action,string text){try{action();}catch(ArgumentException){Check(true,text);return;}catch(InvalidDataException){Check(true,text);return;}throw new Exception("Accepted "+text);}
    public static void Run()
    {
        var m=new Memory(0x80000,false);var r=new MapRuntimeTerrainReader(m,0x80000);var tile=r.ReadTile(0);
        Check(r.Mode=="node-physx" && r.TileCount==1 && r.NodeFloatBuffersMissing==1,"Null node float array with positive count can use independent runtime heightfield");
        var p=tile.Vertex(5);Check(p.X==2 && p.Y==1 && p.Z==12,"Non-symmetric raw samples preserve row/column orientation and scaling");
        Check(tile.Triangle(0,0,0,out var a,out var b,out var c)&&a==0&&b==1&&c==3,"Unflagged cell retains physical diagonal");
        m=new Memory(0x80000,false);m.Samples[2]=128;tile=new MapRuntimeTerrainReader(m,0x80000).ReadTile(0);
        Check(tile.Triangle(0,0,0,out a,out b,out c)&&a==3&&b==0&&c==4,"Tessellation flag retains alternate diagonal");
        m.Samples[2]=255;m.Samples[3]=127;tile=new MapRuntimeTerrainReader(m,0x80000).ReadTile(0);
        Check(!tile.Triangle(0,0,0,out a,out b,out c)&&!tile.Triangle(0,0,1,out a,out b,out c),"Both independently flagged hole triangles omitted");
        m.Samples[3]=0;tile=new MapRuntimeTerrainReader(m,0x80000).ReadTile(0);
        Check(!tile.Triangle(0,0,0,out a,out b,out c)&&tile.Triangle(0,0,1,out a,out b,out c),"A half-cell hole does not erase its solid neighbour");
        m=new Memory(0x80000,true);r=new MapRuntimeTerrainReader(m,0x80000);tile=r.ReadTile(0);
        Check(r.Mode=="global-physx" && tile.Vertex(5).Z==17 && tile.Step==1,"Global raw heightfield applies runtime height offset and spacing");
        m=new Memory(0x80000,true);Memory.I(m.Header,0xc,0);Memory.I(m.Header,0x10,0);Memory.F(m.Header,0,0);
        Check(new MapRuntimeTerrainReader(m,0x80000).ReadTile(0).SampleCount==9,"Global physics field does not require unused node dimensions");
        m=new Memory(0x80000,false);m.Samples[0]=0;m.Samples[1]=128;tile=new MapRuntimeTerrainReader(m,0x80000).ReadTile(0);
        Check(tile.Vertex(0).Z==-16384,"Signed 16-bit heights are not decoded as unsigned");
        Reject(()=>new MapTerrainHeightfield(1,3,new byte[12],0,0,1,1,0),"Reject invalid grid dimensions");
        Reject(()=>new MapTerrainHeightfield(3,3,new byte[35],0,0,1,1,0),"Reject incomplete sample snapshot");
        m=new Memory(0x80000,false);Memory.U(m.Provider,0,0);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject unknown native provider vtable");
        m=new Memory(0x80000,false);Memory.U(m.Physx,0,0);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject unknown PhysX vtable");
        m=new Memory(0x80000,false);Memory.I(m.Physx,0x60,2);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject unsupported sample format");
        m=new Memory(0x80000,false);Memory.U(m.Physx,0x50,0);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject null sample pointer without native function call");
        m=new Memory(0x80000,false);Memory.I(m.Physx,0x38,int.MaxValue);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject overflowing dimensions before sample allocation");
        m=new Memory(0x80000,false);Memory.F(m.Provider,0x10,float.NaN);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject nonfinite height scale");
        m=new Memory(0x80000,false);m.AfterSamples=()=>Memory.I(m.Physx,0x78,1);r=new MapRuntimeTerrainReader(m,0x80000);
        Reject(()=>r.ReadTile(0),"Reject timestamp change during copy");
        Check(r.AttemptedSamples==9,"Rejected snapshot still consumes the reserved read budget");
        m=new Memory(0x80000,false);r=new MapRuntimeTerrainReader(m,0x80000,9);r.ReadTile(0);
        Reject(()=>r.ReadTile(0),"Read quota rejects another tile before payload copy");
        Check(m.SampleReads==1&&r.BudgetExhausted,"Budget exhaustion cannot repeatedly read full payloads");
        m=new Memory(0x80000,false);m.AfterSamples=()=>Memory.U(m.Physx,0x50,0x150004);Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Reject sample pointer replacement during copy");
        m=new Memory(0x80000,false);m.ShortSamples=true;Reject(()=>new MapRuntimeTerrainReader(m,0x80000).ReadTile(0),"Unreadable or partial native memory is a managed failure");
        var source=new Scene{Height=v=>v.x+10*v.y};var target=new Scene();m=new Memory(source.Pointer.ToUInt64(),false);
        var live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,2,2),true,false,true,()=>m);
        for(var i=0;i<1000&&!live.InitialFinished;++i)live.Update(.01f);
        Check(live.InitialFinished && !live.Partial && live.TerrainSource=="raw-physx" && source.HeightReads==5 && source.HoleCheckedHeightReads==0,"Collector uses raw samples, five unfiltered height queries validate coordinates independently of holes");
        Check(target.Entities.Count==1&&MeshBuilder.Faces==8&&MeshBuilder.Corners==9,"Terrain shares original grid vertices with indexed topology");
        var reads=m.Reads;live.Dispose();live.Update(1);Check(m.Reads==reads,"Cancelled collector does not read runtime memory again");
        source=new Scene{Height=v=>999};target=new Scene();m=new Memory(source.Pointer.ToUInt64(),false);
        live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,2,2),true,false,true,()=>m);
        for(var i=0;i<1000&&!live.InitialFinished;++i)live.Update(.01f);
        Check(live.Partial&&target.Entities.Count==0&&source.HeightReads==5,"Wrong coordinate mapping is rejected, never replaced with sampled geometry");live.Dispose();
        source=new Scene{Height=v=>v.x+10*v.y+5};m=new Memory(source.Pointer.ToUInt64(),true);
        live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,()=>m);
        for(var i=0;i<1000&&!live.InitialFinished;++i)live.Update(.01f);
        Check(live.InitialFinished&&!live.Partial&&source.HeightReads==5,"Raw physics field can load when render-terrain metadata is unavailable");live.Dispose();
        source=new Scene{Height=v=>v.x+10*v.y};target=new Scene();m=new Memory(source.Pointer.ToUInt64(),false,257);
        live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,256,256),true,false,true,()=>m);
        for(var i=0;i<1000&&target.Entities.Count==0;++i)live.Update(.01f);
        Check(target.Entities.Count>0&&!live.InitialFinished,"Cancellation fixture reaches published terrain while extraction is still pending");
        Check(live.TerrainPercent>0&&live.TerrainPercent<100,"Single global-size tile reports gradual cell progress while publishing chunks");
        var published=target.Entities.ConvertAll(e=>e.Added);reads=m.Reads;target.ClearAll();live.Dispose();live.Update(1);
        Check(m.Reads==reads&&target.Entities.Count==0&&published.TrueForAll(mesh=>mesh.Releases==1),"Mid-extraction teardown detaches entities, frees published meshes and prevents further reads");
        source=new Scene{Height=v=>v.x+10*v.y};target=new Scene();m=new Memory(source.Pointer.ToUInt64(),false,129);
        live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,128,128),false,false,true,()=>m,true);
        for(var i=0;i<1000&&!live.InitialFinished;++i)live.Update(.01f);
        Check(live.InitialFinished&&!live.Partial&&source.HeightReads==5&&target.Entities.Count==4&&live.SourceTerrainTriangles==32768&&live.TerrainTriangles==8192,"Optimized raw collector publishes four batches and 75 percent fewer faces without more height queries");live.Dispose();
        var own=Marshal.AllocHGlobal(16);
        try
        {
            Marshal.Copy(new byte[]{1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16},0,own,16);
            var copied=MapSelfTerrainMemory.ReadCurrentProcess(unchecked((ulong)own.ToInt64()),16);
            Check(copied[0]==1&&copied[15]==16,"Actual self-process read copies an owned buffer without engine execution");
            Reject(()=>MapSelfTerrainMemory.ReadCurrentProcess(0x7ffffffffffffffe,16),"Invalid native address becomes managed read failure without access violation");
        }
        finally { Marshal.FreeHGlobal(own); }
        Console.WriteLine("PASS: "+checks+" raw-heightfield checks; simulated memory, not engine execution");
    }
    internal static void Benchmark(string report)
    {
        var rows=new List<object>();
        foreach(var simplify in new[]{false,true})for(var run=0;run<3;++run)
        {
            var source=new Scene{Height=v=>v.x+10*v.y};var target=new Scene();var memory=new Memory(source.Pointer.ToUInt64(),false,129);
            var rules=new MapLiveGeometryRules(0,0,128,128);var material=new Material();
            var allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
            var live=new MapLiveSceneGeometry(source,target,material,rules,false,false,true,()=>memory,simplify);
            for(var i=0;i<1000&&!live.InitialFinished;++i)live.Update(.01f);
            var ms=watch.Elapsed.TotalMilliseconds;var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            if(!live.InitialFinished||live.Partial)throw new Exception("Collector benchmark incomplete");
            var meshes=target.Entities.ConvertAll(e=>new {signature=e.Added.UploadSignature.ToString("x16"),vertices=e.Added.UploadVertices,faces=e.Added.UploadFaces});
            rows.Add(new {simplify,run,wallMs=ms,allocatedBytes=bytes,displayFaces=live.TerrainTriangles,sourceFaces=live.SourceTerrainTriangles,meshes});live.Dispose();
        }
        System.IO.File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    internal sealed class Memory : IMapTerrainMemory
    {
        public ulong NativeBase=>0x180000000;
        public ulong PhysxBase=>0x190000000;
        public byte[] Header=new byte[0x70],Provider=new byte[0x18],Physx=new byte[0x80],Samples=new byte[36];
        readonly Dictionary<ulong,byte[]> blocks=new Dictionary<ulong,byte[]>();
        public Action AfterSamples; public bool ShortSamples;public int Reads,SampleReads;
        public Memory(ulong scene,bool global,int axis=3)
        {
            var root=new byte[8];U(root,0,0x100000);blocks[scene+0x2bf0]=root;blocks[0x100000]=Header;
            F(Header,0,axis-1);I(Header,0xc,1);I(Header,0x10,1);U(Header,0x40,0x110000);Header[0x68]=(byte)(global?1:0);
            U(Header,0x50,0x130000);F(Header,0x58,axis-1);F(Header,0x5c,5);I(Header,0x60,axis-1);
            var row=new byte[8];U(row,0,0x120000);blocks[0x110000]=row;
            var node=new byte[0x78];I(node,0xc,axis);F(node,0x10,1);U(node,0x68,0x130000);blocks[0x120000]=node;
            U(Provider,0,NativeBase+0xb089b8);U(Provider,8,0x140000);F(Provider,0x10,.5f);blocks[0x130000]=Provider;
            U(Physx,0,PhysxBase+0x129ea0);I(Physx,0x38,axis);I(Physx,0x3c,axis);I(Physx,0x60,1);U(Physx,0x50,0x150000);blocks[0x140000]=Physx;
            Samples=new byte[axis*axis*4];
            for(var y=0;y<axis;++y)for(var x=0;x<axis;++x){var value=(short)(2*(x+10*y));Samples[(y*axis+x)*4]=(byte)value;Samples[(y*axis+x)*4+1]=(byte)(value>>8);}
            blocks[0x150000]=Samples;
        }
        public byte[] Read(ulong address,int size)
        {
            ++Reads;
            if(address==0x150000 && ShortSamples)throw new InvalidDataException("fake short read");
            foreach(var pair in blocks)
            {
                if(address<pair.Key || address+(ulong)size>pair.Key+(ulong)pair.Value.Length)continue;
                var result=new byte[size];Buffer.BlockCopy(pair.Value,(int)(address-pair.Key),result,0,size);
                if(address>=0x150000&&address<0x150000+(ulong)Samples.Length){++SampleReads;AfterSamples?.Invoke();}return result;
            }
            throw new InvalidDataException("fake unreadable memory");
        }
        public static void U(byte[] b,int offset,ulong value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,b,offset,8);
        public static void I(byte[] b,int offset,int value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,b,offset,4);
        public static void F(byte[] b,int offset,float value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,b,offset,4);
    }
}
