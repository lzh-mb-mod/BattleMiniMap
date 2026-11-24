using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

#if !RUNTIME_MAIN_TESTS
class Program
{
    static int checks;
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); ++checks; Console.WriteLine("PASS: "+message); }
    static void Main(string[] args)
    {
        PreviewControlsChecks.Run();
        CoverageChecks.Run(args[0]);
        LiveChecks.Run(args[0]);
        RawTerrainChecks.Run();
        TerrainOptimizationChecks.Run();
        TerrainVerificationChecks.Run();
        ExtractionDiagnosticChecks.Run();
        ObjectProfilingChecks.Run();
        PrimitiveAllocationChecks.Run(args.Length>1?args[1]+".primitives.json":null);
        TerrainModuleChecks.Run(args.Length>1?args[1]+".modules.json":null);
        if(args.Length>1){TerrainOptimizationChecks.ExistingSamples(args);RawTerrainChecks.Benchmark(args[1]+".collector.json");}
        var config=BattleMiniMapConfig.Get();
        config.UseLiveScenePreview=false;
        config.UseRawRuntimeTerrain=false;
        config.SimplifyRuntimeTerrain=false;
        config.ProfileMapGeometry=false;
        Scene.Created=0;
        var source=new MapGeometryRenderer { Data=MapGeometryData.Load(args[0]) };
        var preview=new MapGeometryPreviewRenderer();
        preview.Update(source); Check(Scene.Created==0 && source.Reads==0,"Closed preview neither loads cache nor creates scene");
        config.ShowGeometryPreview=true;
        preview.Update(new MapGeometryRenderer()); Check(Scene.Created==0 && !preview.Available,"Unavailable cache leaves closeable empty panel without native allocation");
        config.ShowGeometryPreview=false;preview.Update(source);config.ShowGeometryPreview=true;
        preview.Update(source); var target=TableauView.Last; var view=target.NativeView; var camera=view.Camera;
        Check(view.Scene.Entities[0].Added.Material.GetShader().Name=="simple_shader","Old bridge preview keeps default simple_shader route");
        Check(preview.Available && MeshBuilder.Faces==218,"Verified proxy uploaded to independent preview");
        target.Pump(); Check(view.Skip && view.Continuous,"Interactive callback stops until next update permits");
        var meshBuilds=MeshBuilder.Builds;
        var eye=camera.Eye; preview.Orbit.Orbit(100,50); preview.Update(source);
        Check(!view.Skip && camera.Eye.x!=eye.x && MeshBuilder.Builds==meshBuilds,"Camera changes redraw without mesh upload");
        var scenes=Scene.Created;
        for(var k=0;k<8;++k)preview.Orbit.Zoom(10);preview.Update(source);
        Check(preview.Orbit.Distance==.02f && Math.Abs(camera.Near-.0002f)<1e-8f && camera.Near<preview.Orbit.Distance && camera.Far==64 &&
            TableauView.Last==target && Scene.Created==scenes && MeshBuilder.Builds==meshBuilds,
            "Production preview close zoom passes adaptive near to camera without rebuilding scene, target or meshes");
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
        Check(broken.Problem==MapPreviewProblem.InitializationFailed,"Allocation failure retains initialization category");
        before=Scene.Created;broken.Update(source);Check(Scene.Created==before,"Initialization failure cannot auto retry");
        Texture.FailWhite=false; config.ShowGeometryPreview=false; broken.Update(source); config.ShowGeometryPreview=true; broken.Update(source);
        Check(broken.Available,"Explicit close and reopen retries failed preview");
        var failedTarget=TableauView.Last; failedTarget.NativeView.Throw=true; failedTarget.Paint(); broken.Update(source);
        Check(!broken.Available && !failedTarget.NativeView.Enabled,"Callback failure disables only preview");
        Check(broken.Problem==MapPreviewProblem.RenderingFailed,"Callback failure retains rendering category");
        before=Scene.Created;broken.Update(source);Check(Scene.Created==before,"Rendering failure cannot auto retry");
        broken.Dispose();
        Console.WriteLine("PASS: "+checks+" actual preview renderer checks with fake native boundary; no GPU execution proof");
    }
}

#endif
namespace BattleMiniMap.Config
{
    class BattleMiniMapConfig
    {
        static readonly BattleMiniMapConfig Value=new BattleMiniMapConfig(); public static BattleMiniMapConfig Get()=>Value;
        public bool ShowRuntimeTerrain=true, UseRuntimeMainMap=true, UseGpuMapGeometry=true, EnableMapGeometry=true, TintGpuMapGeometry, FollowMode, EnableDynamicScale, MapGpuDiagnosticDepth;
        public int WidgetWidth=400, MapGpuDiagnosticMode; public float FollowModeScale;
        public bool ShowGeometryPreview, CutMapGeometryByHeight, UseMapGeometryProxy=true, UseLiveScenePreview, UseCollisionScenePreview, UseRawRuntimeTerrain, SimplifyRuntimeTerrain, ProfileMapGeometry; public float MapGeometryCeiling;
    }
}
namespace BattleMiniMap.View.MapGeometry
{
    #if !RUNTIME_MAIN_TESTS
    class MapGeometryRenderer { public MapGeometryData Data; public int Reads; public MapGeometryData EnsureVerifiedCache(){++Reads;return Data;} }
    #endif
    #if !RUNTIME_MAIN_TESTS
    static class MapGeometryShader {
        public static void Bind(TaleWorlds.Engine.Material material){material.SetShader(TaleWorlds.Engine.Shader.GetFromResource("simple_shader"));}
        public static void BindRuntimeGeometry(TaleWorlds.Engine.Material material){material.SetShader(TaleWorlds.Engine.Shader.GetFromResource("notexture"));}
    }
    #endif
}
namespace TaleWorlds.Library
{
    public static class Debug { public static readonly System.Collections.Generic.List<string> Messages=new System.Collections.Generic.List<string>(); public static void Print(string value){Messages.Add(value);} }
    public struct Vec2 { public float x,y; public Vec2(float x,float y){this.x=x;this.y=y;} public static Vec2 Zero=>new Vec2(); public static bool operator ==(Vec2 a,Vec2 b)=>a.x==b.x&&a.y==b.y; public static bool operator !=(Vec2 a,Vec2 b)=>!(a==b); public override bool Equals(object o)=>o is Vec2 v&&this==v; public override int GetHashCode()=>HashCode.Combine(x,y); public static Vec2 operator -(Vec2 a,Vec2 b)=>new Vec2(a.x-b.x,a.y-b.y); public static Vec2 operator /(Vec2 a,float b)=>new Vec2(a.x/b,a.y/b); }
    public struct Vec2i { public int X,Y; }
    public struct Vec3
    {
        public float x,y,z; public Vec3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vec3 Forward=>new Vec3(0,1,0); public static Vec3 Up=>new Vec3(0,0,1); public float Length=>(float)Math.Sqrt(x*x+y*y+z*z);
        public void Normalize(){var n=Length;x/=n;y/=n;z/=n;}
        public static Vec3 operator /(Vec3 a,float n)=>new Vec3(a.x/n,a.y/n,a.z/n);
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
        public Vec3 BoundMin=new Vec3(-100,-100,-20), BoundMax=new Vec3(100,100,80);
        public float TerrainMin=-10,TerrainMax=10; public bool TerrainRangeAvailable=true;
        public int BoundsReads,TerrainRangeReads;
        public void GetBoundingBox(out Vec3 min,out Vec3 max){++BoundsReads;min=BoundMin;max=BoundMax;}
        public bool GetTerrainMinMaxHeight(out float min,out float max){++TerrainRangeReads;min=TerrainMin;max=TerrainMax;return TerrainRangeAvailable;}
        public Func<Vec2,float> Height;
        public void GetEntities(ref System.Collections.Generic.List<GameEntity> result){++Reads;result.AddRange(Entities);}
        public Vec2i TerrainDimensions;public float TerrainNodeSize;
        public void GetTerrainData(out Vec2i d,out float size,out int layers,out int version){d=TerrainDimensions;size=TerrainNodeSize;layers=0;version=0;}
        public int HoleCheckedHeightReads;
        public float GetTerrainHeight(Vec2 p,bool checkHoles){++HeightReads;if(checkHoles)++HoleCheckedHeightReads;return Height==null?p.x+p.y:Height(p);}

        public static Scene CreateNewScene(bool initialize_physics,bool enable_decals){++Created;return new Scene();}
        public void DisableStaticShadows(bool value){} public void SetName(string value){} public void EnsurePostfxSystem(){} public void SetShadow(bool value){}
        public void ClearAll(){if(ThrowOnClear)throw new Exception("fake clear failure");Entities.Clear();} public void ManualInvalidate(){++Invalidated;}
    }
    public class Camera { public static int Releases; public Vec3 Eye; public MatrixFrame Frame; public float Right,Bottom,Near,Far; public float Aspect; public void SetViewVolume(bool perspective,float left,float right,float bottom,float top,float near,float far){Right=right;Bottom=bottom;Near=near;Far=far;} public Vec3 WorldPointToViewPortPoint(ref Vec3 p)=>new Vec3((p.x-Eye.x)/Right,(p.y-Eye.y)/Bottom,0); public static Camera CreateCamera()=>new Camera(); public void LookAt(Vec3 eye,Vec3 at,Vec3 up){Eye=eye;} public void SetFovVertical(float f,float a,float n,float z){Aspect=a;Near=n;Far=z;} public void ReleaseCamera(){++Releases;} }
    public class Texture
    {
        public static bool FailWhite;
        private bool _valid=true, _released; private object _userData;
        public int ValidGets, ReleasedGets, UserGets, Releases;
        public bool IsValid { get { ++ValidGets; return _valid; } set { _valid=value; } }
        public bool IsReleased { get { ++ReleasedGets; return _released; } set { _released=value; } }
        public object UserData { get { ++UserGets; return _userData; } set { _userData=value; } }
        public int ViewGets;
        public TableauView NativeView; public Action<Texture,EventArgs> Callback;
        public TableauView TableauView { get { ++ViewGets; return NativeView; } }
        public static Texture CreateFromByteArray(byte[] bytes,int w,int h)=>FailWhite?null:new Texture();
        public void Release(){++Releases;IsReleased=true;} public void Paint()=>Callback(this,EventArgs.Empty);
        public void Pump(){if(NativeView.Enabled&&NativeView.Continuous&&!NativeView.Skip)Paint();}
    }
    [Flags] public enum MaterialFlags { TwoSided=1,DontCastShadow=2,RequiresForwardRendering=4 }
    public class Material
    {
        public enum MBTextureType { DiffuseMap } public enum MBAlphaBlendMode { NoAlphaBlend }
        public MaterialFlags Flags; public static Material GetDefaultMaterial()=>new Material(); public Material CreateCopy()=>new Material();
        private Shader _shader;private ulong _shaderFlags;private MBAlphaBlendMode _blend;
        public int ShaderFlagsReads,AlphaBlendReads;public Shader OverrideReadback;
        public void SetShader(Shader shader){_shader=shader;}public Shader GetShader()=>OverrideReadback??_shader;
        public ulong GetShaderFlags(){++ShaderFlagsReads;return _shaderFlags;}public MBAlphaBlendMode GetAlphaBlendMode(){++AlphaBlendReads;return _blend;}
        public void SetShaderFlags(ulong v){_shaderFlags=v;} public void SetTexture(MBTextureType t,Texture v){} public void SetAlphaBlendMode(MBAlphaBlendMode v){_blend=v;} public void SetAlphaTestValue(int v){} public void ManualInvalidate(){}
    }
    public class Shader {
        public bool IsValid=true;public string Name;public static Func<string,Shader> Resolver;
        public static Shader GetFromResource(string name)=>Resolver==null?new Shader{Name=name}:Resolver(name);
    }
    public static class NativeProbe { public static Action<string> Before; public static void Call(string name){Before?.Invoke(name);} }
    public class MeshBuilder
    {
        public static bool RecordColors; public static readonly System.Collections.Generic.List<uint> UploadedColors=new System.Collections.Generic.List<uint>();
        public static int Builds,Faces,Corners; int _faces,_corners;
        private ulong _signature=14695981039346656037UL;
        private void Mix(uint value){unchecked{_signature=(_signature^value)*1099511628211UL;}}
        public int AddFaceCorner(Vec3 p,Vec3 n,Vec2 uv,uint color){if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z))throw new Exception("nonfinite native corner");if(RecordColors)UploadedColors.Add(color);Mix(0x56455254);Mix((uint)BitConverter.SingleToInt32Bits(p.x));Mix((uint)BitConverter.SingleToInt32Bits(p.y));Mix((uint)BitConverter.SingleToInt32Bits(p.z));Mix(color);return _corners++;}
        public void AddFace(int a,int b,int c){Mix(0x46414345);Mix((uint)a);Mix((uint)b);Mix((uint)c);++_faces;}
        public Mesh Finalize(){NativeProbe.Call("Upload");++Builds;Faces=_faces;Corners=_corners;return new Mesh{UploadSignature=_signature,UploadFaces=_faces,UploadVertices=_corners};}
    }
    public class Mesh {
        public UIntPtr Pointer {get;}=Ids.Next(); public bool IsValid=true; public uint Color;
        public ulong UploadSignature;public int UploadFaces,UploadVertices;
        public MatrixFrame Local=MatrixFrame.Identity; public Material Material; public int Copies, Releases;
        public Mesh CreateCopy(){NativeProbe.Call("RenderCopy");++Copies;return new Mesh{Local=Local};}
        public MatrixFrame GetLocalFrame()=>Local; public void SetLocalFrame(MatrixFrame f){Local=f;}
        public uint GetFaceCount()=>12; public void UpdateBoundingBox(){} public void SetMaterial(Material value){Material=value;}
        public void ManualInvalidate(){++Releases;}
    }
    public class GameEntity {
        public UIntPtr Pointer {get;}=Ids.Next(); public Scene Scene; public bool Visible=true; public object Skeleton;
        public PhysicsShape Shape; public MatrixFrame Frame=MatrixFrame.Identity;
        public readonly System.Collections.Generic.List<MetaMesh> Metas=new System.Collections.Generic.List<MetaMesh>();
        public Mesh Added;
        public static GameEntity CreateEmpty(Scene s,bool createPhysics,bool callScriptCallbacks){NativeProbe.Call("CreateInstance");var e=new GameEntity{Scene=s};s.Entities.Add(e);return e;}
        public UIntPtr GetScenePointer()=>Scene.Pointer; public bool IsVisibleIncludeParents()=>Visible;
        public MatrixFrame GetGlobalFrame()=>Frame; public MatrixFrame GetBodyWorldTransform(){NativeProbe.Call("BodyFrame");return Frame;}
        public int MultiMeshComponentCount=>Metas.Count; public MetaMesh GetMetaMesh(int i)=>Metas[i]; public PhysicsShape GetBodyShape()=>Shape;
        public void SetFrame(ref MatrixFrame f){if(!float.IsFinite(f.origin.x)||!float.IsFinite(f.origin.y)||!float.IsFinite(f.origin.z)||!float.IsFinite(f.rotation.s.x)||!float.IsFinite(f.rotation.s.y)||!float.IsFinite(f.rotation.s.z)||!float.IsFinite(f.rotation.f.x)||!float.IsFinite(f.rotation.f.y)||!float.IsFinite(f.rotation.f.z)||!float.IsFinite(f.rotation.u.x)||!float.IsFinite(f.rotation.u.y)||!float.IsFinite(f.rotation.u.z))throw new Exception("nonfinite native frame");NativeProbe.Call("UpdateInstance");Frame=f;} public void AddMesh(Mesh m,bool recomputeBoundingBox){Added=m;}
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
        public PhysicsShape CreateCopy(){NativeProbe.Call("CopyShape");++Copies;return new PhysicsShape{Original=this,Ready=Ready,MeshN=MeshN,SphereN=SphereN,CapsuleN=CapsuleN,TriangleKind=TriangleKind,TestRadius=TestRadius,TestOrigin=TestOrigin};}
        public void Prepare(){NativeProbe.Call("PrepareShape");++Prepared;}
        public void GetTriangle(Vec3[] t,int m,int i){NativeProbe.Call("TriangleBuild");++Original.Reads;t[0]=new Vec3(0,0,0);t[1]=new Vec3(1,0,0);t[2]=TriangleKind==1?t[1]:TriangleKind==2?new Vec3(float.NaN,1,0):new Vec3(0,1,0);}
        public void ManualInvalidate(){NativeProbe.Call("ReleaseShape");++Released;}
    }
    public class View { public enum ViewRenderOptions { ClearColor,ClearDepth } }
    public class TableauView
    {
        public static Texture Last; public bool Skip,Enabled,Continuous,Throw; public Camera Camera; public Scene Scene;
        public int Width,Height;
        public static Texture AddTableau(string name,Action<Texture,EventArgs> paint,object data,int w,int h)=>Last=new Texture{UserData=data,NativeView=new TableauView{Width=w,Height=h},Callback=paint};
        public Action BeforeSetScene; public void SetScene(Scene s){BeforeSetScene?.Invoke();Scene=s;if(Throw)throw new Exception("fake callback failure");} public void SetCamera(Camera v){Camera=v;}
        public void SetClearColor(uint v){} public void SetRenderOption(View.ViewRenderOptions o,bool v){}
        public void SetCleanScreenUntilLoadingDone(bool v){} public void SetClearAndDisableAfterSucessfullRender(bool v){}
        public void SetRenderWithPostfx(bool v){} public void SetSceneUsesSkybox(bool v){} public void SetSceneUsesShadows(bool v){} public void SetSceneUsesContour(bool v){}
        public void SetAcceptGlobalDebugRenderObjects(bool v){} public void SetRenderOrder(int v){} public void SetDeleteAfterRendering(bool v){}
        public void SetContinuousRendering(bool v){Continuous=v;} public void SetEnable(bool v){Enabled=v;} public void SetDoNotRenderThisFrame(bool v){Skip=v;} public void AddClearTask(bool clearOnlySceneview){}
    }
}
