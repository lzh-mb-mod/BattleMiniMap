using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

class Program
{
    static int checks;
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); ++checks; Console.WriteLine("PASS: "+message); }
    static void Main(string[] args)
    {
        LiveChecks.Run(args[0]);
        RawTerrainChecks.Run();
        TerrainOptimizationChecks.Run();
        TerrainVerificationChecks.Run();
        if(args.Length>1)TerrainOptimizationChecks.ExistingSamples(args);
        var config=BattleMiniMapConfig.Get();
        config.UseLiveScenePreview=false;
        config.UseRawRuntimeTerrain=false;
        config.SimplifyRuntimeTerrain=false;
        Scene.Created=0;
        var source=new MapGeometryRenderer { Data=MapGeometryData.Load(args[0]) };
        var preview=new MapGeometryPreviewRenderer();
        preview.Update(source); Check(Scene.Created==0 && source.Reads==0,"Closed preview neither loads cache nor creates scene");
        config.ShowGeometryPreview=true;
        preview.Update(new MapGeometryRenderer()); Check(Scene.Created==0 && !preview.Available,"Unavailable cache leaves closeable empty panel without native allocation");
        preview.Update(source); var target=TableauView.Last; var view=target.NativeView; var camera=view.Camera;
        Check(preview.Available && MeshBuilder.Faces==218,"Verified proxy uploaded to independent preview");
        target.Pump(); Check(view.Skip && view.Continuous,"Interactive callback stops until next update permits");
        var meshBuilds=MeshBuilder.Builds;
        var eye=camera.Eye; preview.Orbit.Orbit(100,50); preview.Update(source);
        Check(!view.Skip && camera.Eye.x!=eye.x && MeshBuilder.Builds==meshBuilds,"Camera changes redraw without mesh upload");
        config.CutMapGeometryByHeight=true; config.MapGeometryCeiling=float.NaN;
        preview.Update(source); preview.Update(source);
        Check(TableauView.Last==target,"Nonfinite height cannot cause dirty-target rebuild loop");
        config.CutMapGeometryByHeight=false;
        config.UseMapGeometryProxy=false; preview.Update(source);
        var next=TableauView.Last;
        Check(next!=target && MeshBuilder.Faces==3242 && !view.Enabled && !view.Continuous,"Original switch retires old target and uploads original");
        var gets=target.ViewGets; target.Paint(); Check(target.ViewGets==gets,"Retired callback cannot access native view");
        preview.Update(source); preview.Update(source); Check(target.IsReleased,"Old UI target released after deferred updates");
        config.CutMapGeometryByHeight=true; config.MapGeometryCeiling=-1000; preview.Update(source);
        Check(preview.Available && TableauView.Last!=next && MeshBuilder.Builds==meshBuilds+1,"All-clipped geometry creates clean empty target without stale mesh");
        var empty=TableauView.Last;
        config.ShowGeometryPreview=false; preview.Update(source);
        Check(!preview.Available && !empty.NativeView.Enabled && !empty.NativeView.Continuous,"Close removes texture and stops continuous rendering");
        gets=empty.ViewGets; empty.Paint(); Check(empty.ViewGets==gets,"Closed target rejects late callback");
        config.ShowGeometryPreview=true; config.CutMapGeometryByHeight=false; config.UseMapGeometryProxy=true;
        preview.Update(source); var reopened=TableauView.Last;
        Check(reopened!=empty && preview.Orbit.Distance==4.5f,"Reopen creates new lifetime and resets camera");
        var before=Camera.Releases; Scene.ThrowOnClear=true; preview.Dispose();
        Check(reopened.IsReleased && Camera.Releases>before && MapGeometryPreviewRenderer.Active==null && !config.ShowGeometryPreview,"Cleanup exception does not prevent target/camera release or session reset");
        Scene.ThrowOnClear=false;
        config.ShowGeometryPreview=true; Texture.FailWhite=true; var broken=new MapGeometryPreviewRenderer();
        before=Scene.Invalidated; broken.Update(source);
        broken.Update(source); broken.Update(source);
        Check(!broken.Available && Scene.Invalidated>before,"Partial allocation failure isolated and released");
        Texture.FailWhite=false; config.ShowGeometryPreview=false; broken.Update(source); config.ShowGeometryPreview=true; broken.Update(source);
        Check(broken.Available,"Explicit close and reopen retries failed preview");
        var failedTarget=TableauView.Last; failedTarget.NativeView.Throw=true; failedTarget.Paint(); broken.Update(source);
        Check(!broken.Available && !failedTarget.NativeView.Enabled,"Callback failure disables only preview");
        broken.Dispose();
        Console.WriteLine("PASS: "+checks+" actual preview renderer checks with fake native boundary; no GPU execution proof");
    }
}

namespace BattleMiniMap.Config
{
    class BattleMiniMapConfig
    {
        static readonly BattleMiniMapConfig Value=new BattleMiniMapConfig(); public static BattleMiniMapConfig Get()=>Value;
        public bool ShowGeometryPreview, CutMapGeometryByHeight, UseMapGeometryProxy=true, UseLiveScenePreview, UseCollisionScenePreview, UseRawRuntimeTerrain, SimplifyRuntimeTerrain; public float MapGeometryCeiling;
    }
}
namespace BattleMiniMap.View.MapGeometry
{
    class MapGeometryRenderer { public MapGeometryData Data; public int Reads; public MapGeometryData EnsureVerifiedCache(){++Reads;return Data;} }
    static class MapGeometryShader { public static void Bind(TaleWorlds.Engine.Material material){} }
}
namespace TaleWorlds.Library
{
    public static class Debug { public static readonly System.Collections.Generic.List<string> Messages=new System.Collections.Generic.List<string>(); public static void Print(string value){Messages.Add(value);} }
    public struct Vec2 { public float x,y; public Vec2(float x,float y){this.x=x;this.y=y;} }
    public struct Vec2i { public int X,Y; }
    public struct Vec3
    {
        public float x,y,z; public Vec3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vec3 Up=>new Vec3(0,0,1); public float Length=>(float)Math.Sqrt(x*x+y*y+z*z);
        public void Normalize(){var n=Length;x/=n;y/=n;z/=n;}
        public static Vec3 operator -(Vec3 a,Vec3 b)=>new Vec3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vec3 operator +(Vec3 a,Vec3 b)=>new Vec3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vec3 operator *(Vec3 a,float n)=>new Vec3(a.x*n,a.y*n,a.z*n);
    }
    public struct Color { public Color(float r,float g,float b,float a){} public uint ToUnsignedInteger()=>1; }
}
namespace TaleWorlds.Engine.GauntletUI { public class EngineTexture { public TaleWorlds.Engine.Texture Native; public EngineTexture(TaleWorlds.Engine.Texture t){Native=t;} } }
namespace TaleWorlds.TwoDimension { public class Texture { readonly TaleWorlds.Engine.GauntletUI.EngineTexture _platform; public Texture(TaleWorlds.Engine.GauntletUI.EngineTexture t){_platform=t;} public bool IsValid=>_platform.Native.IsValid&&!_platform.Native.IsReleased; } }
namespace TaleWorlds.Engine
{
    using TaleWorlds.Library;
    public class Scene
    {
        public static int Created,Invalidated; public static bool ThrowOnClear;
        public UIntPtr Pointer { get; }=Ids.Next();
        public readonly System.Collections.Generic.List<GameEntity> Entities=new System.Collections.Generic.List<GameEntity>();
        public int Reads,HeightReads;
        public Func<Vec2,float> Height;
        public void GetEntities(ref System.Collections.Generic.List<GameEntity> result){++Reads;result.AddRange(Entities);}
        public void GetTerrainData(out Vec2i d,out float size,out int layers,out int version){d=new Vec2i();size=0;layers=0;version=0;}
        public int HoleCheckedHeightReads;
        public float GetTerrainHeight(Vec2 p,bool checkHoles){++HeightReads;if(checkHoles)++HoleCheckedHeightReads;return Height==null?p.x+p.y:Height(p);}

        public static Scene CreateNewScene(bool initialize_physics,bool enable_decals){++Created;return new Scene();}
        public void DisableStaticShadows(bool value){} public void SetName(string value){} public void EnsurePostfxSystem(){} public void SetShadow(bool value){}
        public void ClearAll(){if(ThrowOnClear)throw new Exception("fake clear failure");Entities.Clear();} public void ManualInvalidate(){++Invalidated;}
    }
    public class Camera { public static int Releases; public Vec3 Eye; public static Camera CreateCamera()=>new Camera(); public void LookAt(Vec3 eye,Vec3 at,Vec3 up){Eye=eye;} public void SetFovVertical(float f,float a,float n,float z){} public void ReleaseCamera(){++Releases;} }
    public class Texture
    {
        public static bool FailWhite; public bool IsValid=true,IsReleased; public object UserData; public int ViewGets;
        public TableauView NativeView; public Action<Texture,EventArgs> Callback;
        public TableauView TableauView { get { ++ViewGets; return NativeView; } }
        public static Texture CreateFromByteArray(byte[] bytes,int w,int h)=>FailWhite?null:new Texture();
        public void Release(){IsReleased=true;} public void Paint()=>Callback(this,EventArgs.Empty);
        public void Pump(){if(NativeView.Enabled&&NativeView.Continuous&&!NativeView.Skip)Paint();}
    }
    [Flags] public enum MaterialFlags { TwoSided=1,DontCastShadow=2,RequiresForwardRendering=4 }
    public class Material
    {
        public enum MBTextureType { DiffuseMap } public enum MBAlphaBlendMode { NoAlphaBlend }
        public MaterialFlags Flags; public static Material GetDefaultMaterial()=>new Material(); public Material CreateCopy()=>new Material();
        public void SetShaderFlags(int v){} public void SetTexture(MBTextureType t,Texture v){} public void SetAlphaBlendMode(MBAlphaBlendMode v){} public void SetAlphaTestValue(int v){} public void ManualInvalidate(){}
    }
    public class MeshBuilder
    {
        public static int Builds,Faces,Corners; int _faces,_corners;
        public int AddFaceCorner(Vec3 p,Vec3 n,Vec2 uv,uint color)=>_corners++; public void AddFace(int a,int b,int c){++_faces;}
        public Mesh Finalize(){++Builds;Faces=_faces;Corners=_corners;return new Mesh();}
    }
    public class Mesh {
        public UIntPtr Pointer {get;}=Ids.Next(); public bool IsValid=true; public uint Color;
        public MatrixFrame Local=MatrixFrame.Identity; public Material Material; public int Copies, Releases;
        public Mesh CreateCopy(){++Copies;return new Mesh{Local=Local};}
        public MatrixFrame GetLocalFrame()=>Local; public void SetLocalFrame(MatrixFrame f){Local=f;}
        public uint GetFaceCount()=>12; public void UpdateBoundingBox(){} public void SetMaterial(Material value){Material=value;}
        public void ManualInvalidate(){++Releases;}
    }
    public class GameEntity {
        public UIntPtr Pointer {get;}=Ids.Next(); public Scene Scene; public bool Visible=true; public object Skeleton;
        public PhysicsShape Shape; public MatrixFrame Frame=MatrixFrame.Identity;
        public readonly System.Collections.Generic.List<MetaMesh> Metas=new System.Collections.Generic.List<MetaMesh>();
        public Mesh Added;
        public static GameEntity CreateEmpty(Scene s,bool createPhysics,bool callScriptCallbacks){var e=new GameEntity{Scene=s};s.Entities.Add(e);return e;}
        public UIntPtr GetScenePointer()=>Scene.Pointer; public bool IsVisibleIncludeParents()=>Visible;
        public MatrixFrame GetGlobalFrame()=>Frame; public MatrixFrame GetBodyWorldTransform()=>Frame;
        public int MultiMeshComponentCount=>Metas.Count; public MetaMesh GetMetaMesh(int i)=>Metas[i]; public PhysicsShape GetBodyShape()=>Shape;
        public void SetFrame(ref MatrixFrame f){Frame=f;} public void AddMesh(Mesh m,bool recomputeBoundingBox){Added=m;}
        public void SetVisibilityExcludeParents(bool v){Visible=v;} public void SetReadyToRender(bool v){}
    }
    public static class Ids {static ulong id;public static UIntPtr Next()=>new UIntPtr(++id);}
    public class MetaMesh {
        public UIntPtr Pointer {get;}=Ids.Next(); public bool IsValid=true; public MatrixFrame Frame=MatrixFrame.Identity;
        public readonly System.Collections.Generic.List<Mesh> Meshes=new System.Collections.Generic.List<Mesh>();
        public int MeshCount=>Meshes.Count; public int GetLodMaskForMeshAtIndex(int i)=>i==0?1:4; public Mesh GetMeshAtIndex(int i)=>Meshes[i];
    }
    public struct SphereData { public Vec3 Origin;public float Radius; }
    public struct CapsuleData { public Vec3 P1,P2;public float Radius; }
    public class PhysicsShape {
        public UIntPtr Pointer {get;}=Ids.Next(); public bool IsValid=true,Ready=true; public int Reads,Prepared,Released,Copies;
        public int TriangleKind; public float TestRadius=1; public Vec3 TestOrigin;
        public int MeshN=1,SphereN,CapsuleN; public PhysicsShape Original; public int TriangleMeshCount()=>MeshN; public int TriangleCountInTriangleMesh(int i)=>1;
        public int SphereCount()=>SphereN; public int CapsuleCount()=>CapsuleN;
        public void GetSphere(ref SphereData s,int i){s.Origin=TestOrigin;s.Radius=TestRadius;}
        public void GetCapsule(ref CapsuleData c,int i){c.P1=new Vec3(0,0,0);c.P2=new Vec3(0,0,2);c.Radius=.5f;}
        public PhysicsShape CreateCopy(){++Copies;return new PhysicsShape{Original=this,Ready=Ready,MeshN=MeshN,SphereN=SphereN,CapsuleN=CapsuleN,TriangleKind=TriangleKind,TestRadius=TestRadius,TestOrigin=TestOrigin};}
        public void Prepare(){++Prepared;}
        public void GetTriangle(Vec3[] t,int m,int i){++Original.Reads;t[0]=new Vec3(0,0,0);t[1]=new Vec3(1,0,0);t[2]=TriangleKind==1?t[1]:TriangleKind==2?new Vec3(float.NaN,1,0):new Vec3(0,1,0);}
        public void ManualInvalidate(){++Released;}
    }
    public class View { public enum ViewRenderOptions { ClearColor,ClearDepth } }
    public class TableauView
    {
        public static Texture Last; public bool Skip,Enabled,Continuous,Throw; public Camera Camera;
        public static Texture AddTableau(string name,Action<Texture,EventArgs> paint,object data,int w,int h)=>Last=new Texture{UserData=data,NativeView=new TableauView(),Callback=paint};
        public void SetScene(Scene s){if(Throw)throw new Exception("fake callback failure");} public void SetCamera(Camera v){Camera=v;}
        public void SetClearColor(uint v){} public void SetRenderOption(View.ViewRenderOptions o,bool v){}
        public void SetCleanScreenUntilLoadingDone(bool v){} public void SetClearAndDisableAfterSucessfullRender(bool v){}
        public void SetRenderWithPostfx(bool v){} public void SetSceneUsesSkybox(bool v){} public void SetSceneUsesShadows(bool v){} public void SetSceneUsesContour(bool v){}
        public void SetAcceptGlobalDebugRenderObjects(bool v){} public void SetRenderOrder(int v){} public void SetDeleteAfterRendering(bool v){}
        public void SetContinuousRendering(bool v){Continuous=v;} public void SetEnable(bool v){Enabled=v;} public void SetDoNotRenderThisFrame(bool v){Skip=v;} public void AddClearTask(bool clearOnlySceneview){}
    }
}
