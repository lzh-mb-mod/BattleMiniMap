using System;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;
class Program
{
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
 static void Main(){
  var scene=new Scene();var gpu=new MapGeometryTableau(scene,new Camera());gpu.Initialize(32,64);
  var texture=TableauView.Last;var view=texture.NativeView;
  Check(gpu.Texture.IsValid && view.Clear==0,"Main target wraps valid texture and clears transparent");
  Check(!view.Skip,"New dirty target initially requests rendering");
  gpu.SetEnabled(true);texture.Paint();
  Check(gpu.PaintCount==1 && gpu.CallbackCount==1 && !view.Skip && !gpu.HasSubmittedCache,"First paint keeps second cycle permitted; no premature cache");
  var permits=view.PermitWrites;gpu.SetEnabled(true);Check(view.PermitWrites==permits,"Waiting Update does not repeatedly rewrite native permit");
  gpu.SetEnabled(false);var paints=gpu.PaintCount;texture.Paint();Check(gpu.PaintCount==paints,"Hidden callback does not configure or redraw target");
  gpu.SetEnabled(true);texture.Paint();
  Check(gpu.PaintCount==2 && gpu.HasSubmittedCache && view.Skip && view.Enabled,"Second successful callback parks future cycles without disabling current view");
  gpu.SetEnabled(true);permits=view.PermitWrites;
  for(var i=0;i<1000;i++){gpu.SetEnabled(true);texture.Pump();}
  Check(gpu.PaintCount==2 && gpu.CallbackCount==2 && view.PermitWrites==permits,"Cached target stays at two callbacks through1000 fake native pumps");
  Check(gpu.Texture.IsValid && view.Continuous && !texture.IsReleased,"Cached texture and enabled continuous view retained; no target clearing");
  gpu.SetEnabled(false);gpu.SetEnabled(true);texture.Pump();
  Check(view.PermitWrites==permits && gpu.CallbackCount==2 && view.Skip,"Showing cache restores view without granting redraw");
  var configured=scene.Configurations;texture.Paint();
  Check(gpu.CallbackCount==3 && gpu.PaintCount==2 && scene.Configurations==configured && view.Skip,"Unexpected cached callback is observable but cannot reconfigure or keep requesting frames");
  gpu.Retire();var access=texture.ViewGets;texture.Paint();Check(texture.ViewGets==access,"Retired callback returns before fetching native view");
  var next=new MapGeometryTableau(new Scene(),new Camera());next.Initialize(32,64);var fresh=TableauView.Last;
  texture.Paint();Check(next.PaintCount==0 && !fresh.NativeView.Skip,"Dirty generation starts uncached and ignores old callbacks");
  gpu.Release();var userReads=texture.UserGets;texture.Paint();Check(texture.UserGets==userReads && gpu.Texture==null,"Released callback returns before reading userdata");
  fresh.IsValid=false;fresh.Paint();Check(next.PaintCount==0,"Invalid target callback does not touch scene");
  next.Retire();next.Release();
  var brokenScene=new Scene();var broken=new MapGeometryTableau(brokenScene,new Camera());broken.Initialize(32,64);
  var badTexture=TableauView.Last;brokenScene.Throw=true;badTexture.Paint();
  Check(broken.Failure!=null && broken.PaintCount==0 && !broken.HasSubmittedCache,"Callback failure cannot publish cache and is exposed for CPU fallback");
  broken.Retire();broken.Release();
  Console.WriteLine("PASS: "+checks+" actual Tableau adapter checks with fake native boundary; no GPU completion or execution proof");
 }
}
namespace TaleWorlds.Library {public static class Debug{public static void Print(string value){}}}
namespace TaleWorlds.Engine.GauntletUI {public class EngineTexture{public TaleWorlds.Engine.Texture Native;public EngineTexture(TaleWorlds.Engine.Texture texture){Native=texture;}}}
namespace TaleWorlds.TwoDimension {public class Texture{readonly TaleWorlds.Engine.GauntletUI.EngineTexture platform;public Texture(TaleWorlds.Engine.GauntletUI.EngineTexture value){platform=value;}public bool IsValid=>platform.Native.IsValid&&!platform.Native.IsReleased;}}
namespace TaleWorlds.Engine {
 public class Scene{public bool Throw;public int Configurations;public void EnsurePostfxSystem(){Configurations++;if(Throw)throw new Exception("fake scene failure");}}
 public class Camera{}
 public class Texture{
  public bool IsValid=true,IsReleased;public int ViewGets,UserGets;public object Data;public TableauView NativeView;public Action<Texture,EventArgs> Callback;
  public object UserData{get{UserGets++;return Data;}}public TableauView TableauView{get{ViewGets++;return NativeView;}}
  public void Pump(){if(NativeView.Enabled&&NativeView.Continuous&&!NativeView.Skip)Paint();}public void Paint()=>Callback(this,EventArgs.Empty);public void Release(){IsReleased=true;}
 }
 public class View{public enum ViewRenderOptions{ClearColor,ClearDepth}}
 public class TableauView:View{
  public static Texture Last;public bool Enabled,Skip,Continuous;public uint Clear;public int PermitWrites;
  public static Texture AddTableau(string name,Action<Texture,EventArgs> callback,object data,int width,int height)=>Last=new Texture{Data=data,Callback=callback,NativeView=new TableauView()};
  public void SetScene(Scene scene){}public void SetCamera(Camera camera){}public void SetClearColor(uint value){Clear=value;}
  public void SetRenderOption(ViewRenderOptions option,bool value){}public void SetCleanScreenUntilLoadingDone(bool value){}
  public void SetClearAndDisableAfterSucessfullRender(bool value){}public void SetRenderWithPostfx(bool value){}
  public void SetSceneUsesSkybox(bool value){}public void SetSceneUsesShadows(bool value){}public void SetSceneUsesContour(bool value){}
  public void SetAcceptGlobalDebugRenderObjects(bool value){}public void SetRenderOrder(int value){}public void SetDeleteAfterRendering(bool value){}
  public void SetContinuousRendering(bool value){Continuous=value;}public void SetEnable(bool value){Enabled=value;}
  public void SetDoNotRenderThisFrame(bool value){Skip=value;if(!value)PermitWrites++;}public void AddClearTask(bool clearOnlySceneview){}
 }
}
