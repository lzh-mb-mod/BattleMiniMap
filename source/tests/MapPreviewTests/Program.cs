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
        var config=BattleMiniMapConfig.Get();
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
        public bool ShowGeometryPreview, CutMapGeometryByHeight, UseMapGeometryProxy=true; public float MapGeometryCeiling;
    }
}
namespace BattleMiniMap.View.MapGeometry
{
    class MapGeometryRenderer { public MapGeometryData Data; public int Reads; public MapGeometryData EnsureVerifiedCache(){++Reads;return Data;} }
    static class MapGeometryShader { public static void Bind(TaleWorlds.Engine.Material material){} }
}
namespace TaleWorlds.Library
{
    public static class Debug { public static void Print(string value){} }
    public struct Vec2 { public Vec2(float x,float y){} }
    public struct Vec3
    {
        public float x,y,z; public Vec3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vec3 Up=>new Vec3(0,0,1); public float Length=>(float)Math.Sqrt(x*x+y*y+z*z);
        public void Normalize(){var n=Length;x/=n;y/=n;z/=n;}
        public static Vec3 operator -(Vec3 a,Vec3 b)=>new Vec3(a.x-b.x,a.y-b.y,a.z-b.z);
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
        public static Scene CreateNewScene(bool initialize_physics,bool enable_decals){++Created;return new Scene();}
        public void DisableStaticShadows(bool value){} public void SetName(string value){} public void EnsurePostfxSystem(){} public void SetShadow(bool value){}
        public void ClearAll(){if(ThrowOnClear)throw new Exception("fake clear failure");} public void ManualInvalidate(){++Invalidated;}
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
        public static int Builds,Faces; int _faces;
        public int AddFaceCorner(Vec3 p,Vec3 n,Vec2 uv,uint color)=>0; public void AddFace(int a,int b,int c){++_faces;}
        public Mesh Finalize(){++Builds;Faces=_faces;return new Mesh();}
    }
    public class Mesh { public uint Color; public void UpdateBoundingBox(){} public void SetMaterial(Material value){} public void ManualInvalidate(){} }
    public class GameEntity { public static GameEntity CreateEmpty(Scene s,bool createPhysics,bool callScriptCallbacks)=>new GameEntity(); public void AddMesh(Mesh m,bool recomputeBoundingBox){} public void SetVisibilityExcludeParents(bool v){} public void SetReadyToRender(bool v){} }
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
