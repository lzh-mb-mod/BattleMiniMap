using System;
using System.Linq;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;
using TaleWorlds.Library;

static class LiveChecks
{
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);++checks;Console.WriteLine("PASS live: "+text);}
    static void Finish(MapLiveSceneGeometry c){for(var i=0;i<1000 && !c.InitialFinished && c.Failure==null;++i)c.Update(.01f);}
    public static void Run(string cache)
    {
        var rules=new MapLiveGeometryRules(0,0,8,8);
        Check(rules.GridX==3 && rules.GridY==3 && MapLiveGeometryRules.CoarsestLod(7)==4,"Bounded runtime terrain grid and LOD membership");
        try {new MapLiveGeometryRules(0,0,float.NaN,1);throw new Exception("NaN accepted");}catch(ArgumentException){}
        Check(MapLiveGeometryRules.CoarsestLod(0)==0,"Unknown LOD is not silently treated as all LODs");
        var sphere=MapLivePrimitiveGeometry.Capsule(new GeometryPoint(0,0,0),new GeometryPoint(0,0,0),2).ToArray();
        Check(sphere.Length>0 && sphere.SelectMany(t=>t).All(p=>Math.Abs(Math.Sqrt(p.X*p.X+p.Y*p.Y+p.Z*p.Z)-2)<1e-5),"Sphere display vertices lie on requested radius without degenerate faces");
        var source=new Scene();var target=new Scene();var material=new Material();
        var e=GameEntity.CreateEmpty(source,false,false); var meta=new MetaMesh();
        var high=new Mesh();var low=new Mesh();meta.Meshes.Add(high);meta.Meshes.Add(low);e.Metas.Add(meta);
        e.Frame.origin=new Vec3(3,2,1);meta.Frame.origin=new Vec3(1,0,0);low.Local.origin=new Vec3(0,1,0);
        var render=new MapLiveSceneGeometry(source,target,material,rules,true,false);Finish(render);
        Check(render.InitialFinished && render.Failure==null && render.CopiedMeshes==1,"Render source extracts actual scene component, selecting one LOD");
        Check(high.Copies==0 && low.Copies==1 && low.Material==null && low.Local.origin.y==1,"Only isolated copies receive map material and local-frame changes");
        var shown=target.Entities[0];
        Check(Math.Abs(shown.Frame.origin.x+.25f)<1e-5 && Math.Abs(shown.Frame.origin.y)<1e-5 && Math.Abs(shown.Frame.origin.z-.25f)<1e-5,"Entity, component and mesh transforms compose before normalized axis mapping");
        Check(source.HeightReads==9 && render.TerrainPercent==100,"Terrain comes from runtime height calls");
        e.Frame.origin=new Vec3(7,2,1);
        for(var i=0;i<10;++i)render.Update(1);
        Check(low.Copies==1 && Math.Abs(shown.Frame.origin.y+1)<1e-5,"Rescan updates instance frame without copying unchanged mesh");
        e.Frame.rotation.s=new Vec3(-2,0,0);for(var i=0;i<10;++i)render.Update(1);
        Check(shown.Frame.rotation.s.y==.5f,"Negative nonuniform source scale survives full affine mapping");
        source.Entities.Clear();for(var i=0;i<10;++i)render.Update(1);
        Check(!shown.Visible,"Removed source instance is hidden after rescan");
        var reads=source.Reads;render.Dispose();render.Update(1);
        Check(source.Reads==reads,"Cancelled extraction performs no further source scans");
        source=new Scene();target=new Scene(); var shape=new PhysicsShape();
        e=GameEntity.CreateEmpty(source,false,false);e.Shape=shape;e.Visible=false;
        var e2=GameEntity.CreateEmpty(source,false,false);e2.Shape=shape;
        var collision=new MapLiveSceneGeometry(source,target,material,rules,false,true);Finish(collision);
        Check(collision.Failure==null && collision.InitialFinished && collision.CopiedMeshes==2,"Collision source includes hidden body definitions and instances shared shape");
        Check(shape.Copies==1 && shape.Reads==1 && shape.Prepared==0,"Shared collision shape read once and source is never prepared");
        var shared=target.Entities[0].Added;collision.Dispose();Check(shared.Releases==1,"Shared collision mesh released exactly once");
        source=new Scene();e=GameEntity.CreateEmpty(source,false,false);e.Shape=new PhysicsShape{Ready=false};
        collision=new MapLiveSceneGeometry(source,new Scene(),material,rules,false,true);Finish(collision);
        Check(collision.Partial && collision.CopiedMeshes==0 && e.Shape.Reads==0,"Missing prepared storage is partial and never read as triangles");collision.Dispose();
        source=new Scene();e=GameEntity.CreateEmpty(source,false,false);e.Shape=new PhysicsShape{Ready=false,MeshN=0,SphereN=1,CapsuleN=1};
        collision=new MapLiveSceneGeometry(source,new Scene(),material,rules,false,true);Finish(collision);
        Check(collision.CopiedMeshes==2 && !collision.Partial && e.Shape.Reads==0,"Pure sphere and capsule bodies generate geometry without triangle-storage access");collision.Dispose();
        MapLiveCollisionGuard.Reject=true;source=new Scene();collision=new MapLiveSceneGeometry(source,new Scene(),material,rules,false,true);Finish(collision);
        Check(collision.Failure!=null && source.Reads==0,"Version guard failure cannot begin scene collision reads");collision.Dispose();MapLiveCollisionGuard.Reject=false;
        var config=BattleMiniMapConfig.Get();config.ShowGeometryPreview=true;config.UseLiveScenePreview=true;config.UseCollisionScenePreview=false;
        source=new Scene();e=GameEntity.CreateEmpty(source,false,false);meta=new MetaMesh();meta.Meshes.Add(new Mesh());e.Metas.Add(meta);
        var cacheProvider=new MapGeometryRenderer{Data=MapGeometryData.Load(cache)};
        var preview=new MapGeometryPreviewRenderer(source,0,0,8,8);preview.Update(cacheProvider);
        Check(preview.Available && cacheProvider.Reads==0,"Runtime preview does not load static bridge cache");
        var first=TableauView.Last;config.UseCollisionScenePreview=true;preview.Update(cacheProvider);
        Check(TableauView.Last!=first && !first.NativeView.Enabled && cacheProvider.Reads==0,"Source switch retires old target without silently reading files");
        config.ShowGeometryPreview=false;preview.Update(cacheProvider);Check(preview.Texture==null,"Runtime preview close detaches visible target");preview.Dispose();
        config.UseLiveScenePreview=false;config.UseCollisionScenePreview=false;
        Console.WriteLine("PASS: "+checks+" runtime-source integration checks with fake native boundary");
    }
}
namespace BattleMiniMap.View.MapGeometry
{
    // The real version-pinned native memory check is separately reviewed, not executed here.
    static class MapLiveCollisionGuard
    {
        public static bool Reject;
        public static void VerifyEngine(){if(Reject)throw new Exception("fake unverified engine");}
        public static bool StorageReady(PhysicsShape shape)=>shape.Ready;
    }
}
namespace TaleWorlds.Library
{
    public struct Mat3
    {
        public Vec3 s,f,u;
        public Vec3 Transform(Vec3 p)=>s*p.x+f*p.y+u*p.z;
    }
    public struct MatrixFrame
    {
        public Vec3 origin; public Mat3 rotation;
        public static MatrixFrame Identity=>new MatrixFrame{rotation=new Mat3{s=new Vec3(1,0,0),f=new Vec3(0,1,0),u=new Vec3(0,0,1)}};
        public static MatrixFrame operator *(MatrixFrame a,MatrixFrame b)=>new MatrixFrame{origin=a.origin+a.rotation.Transform(b.origin),rotation=new Mat3{s=a.rotation.Transform(b.rotation.s),f=a.rotation.Transform(b.rotation.f),u=a.rotation.Transform(b.rotation.u)}};
    }
}
