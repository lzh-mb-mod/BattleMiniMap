using System;
using BattleMiniMap.View.MapGeometry;
using BattleMiniMap.View.Background;
using BattleMiniMap.View.Background.Map;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;
class Program
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
 static void Rejected(Action action,string name){try{action();}catch(InvalidOperationException){Check(true,name);return;}throw new Exception(name);}
 static void Main(){
  var material=new TaleWorlds.Engine.Material();
  TaleWorlds.Engine.Shader.Resolved=new TaleWorlds.Engine.Shader{Name="simple_shader"};
  MapGeometryShader.Bind(material);Check(material.SetCalls==1 && TaleWorlds.Engine.Shader.Requested=="simple_shader","shader resource key and material identity accepted");
  TaleWorlds.Engine.Shader.Resolved=new TaleWorlds.Engine.Shader{Name="adaptive_terrain_compute"};
  Rejected(()=>MapGeometryShader.Bind(material),"valid but wrong native shader fallback rejected");Check(material.SetCalls==1,"wrong resource never assigned to material");
  TaleWorlds.Engine.Shader.Resolved=null;Rejected(()=>MapGeometryShader.Bind(material),"null shader rejected");
  TaleWorlds.Engine.Shader.Resolved=new TaleWorlds.Engine.Shader{Name="simple_shader",IsValid=false};Rejected(()=>MapGeometryShader.Bind(material),"invalid shader rejected");
  TaleWorlds.Engine.Shader.Resolved=new TaleWorlds.Engine.Shader{Name="simple_shader"};
  material.OverrideReadback=new TaleWorlds.Engine.Shader{Name="adaptive_terrain_compute"};Rejected(()=>MapGeometryShader.Bind(material),"wrong material shader readback rejected");

  var config=BattleMiniMap.Config.BattleMiniMapConfig.Get();var ui=new UIContext();var context=new TwoDimensionContext();var draw=new TwoDimensionDrawContext();
  var widget=new BattleMiniMap_GeometryWidget(ui);MapGeometryRenderer.Active=new MapGeometryRenderer{Texture=new Texture()};
  widget.Render(context,draw);Check(draw.Count==1 && widget.TextureProvider==null,"direct full-map draw before any provider bootstrap");
  config.FollowMode=true;widget.Render(context,draw);Check(context.Count==1,"direct follow-map draw before any provider bootstrap");
  MapGeometryRenderer.Active.Texture=new Texture{IsValid=false};widget.Render(context,draw);Check(context.Count==1,"invalid texture not drawn");
  MapGeometryRenderer.Active.Texture=null;widget.Render(context,draw);Check(context.Count==1,"missing texture not drawn");
  MapGeometryRenderer.Active.Texture=new Texture();config.EnableMapGeometry=false;widget.Render(context,draw);Check(context.Count==1,"disabled overlay not drawn");
  config.EnableMapGeometry=true;config.FollowMode=false;var legacy=new MapBackgroundWidget(ui){TextureProvider=new TextureProvider()};legacy.Render(context,draw);Check(draw.Count==2,"existing background provider path preserved");
  var cold=new MapBackgroundWidget(ui);cold.Render(context,draw);Check(cold.BaseRenderCalls==1,"existing background bootstrap preserved");
  var map=new BattleMiniMap_MapWidget(ui){TextureProvider=new TextureProvider(),IsEnabled=true};
  var child=new TaleWorlds.GauntletUI.BaseTypes.TextureWidget(ui){IsEnabled=true};map.Children.Add(child);
  var draws=draw.Count;MapGeometryRenderer.Active.SuppressGdiSelfRender=true;map.Render(context,draw);
  Check(draw.Count==draws && map.TextureProvider.Reads==0,"production MapWidget skips only GDI own image/provider when managed diagnostic state is ready");
  Check(child.BaseRenderCalls==1 && map.IsVisible && map.IsEnabled && child.IsVisible && child.IsEnabled && !map.DoNotAcceptEvents,
   "self-render shortcut preserves child traversal, visibility, enabled state and input acceptance");
  MapGeometryRenderer.Active.SuppressGdiSelfRender=false;map.Render(context,draw);
  Check(draw.Count==draws+1 && child.BaseRenderCalls==2 && map.TextureProvider.Reads==1,"diagnostic close restores GDI own render while children continue");
  MapGeometryRenderer.Active.SuppressGdiSelfRender=true;var coldMap=new BattleMiniMap_MapWidget(ui);coldMap.Children.Add(child);coldMap.Render(context,draw);
  Check(coldMap.BaseRenderCalls==0 && child.BaseRenderCalls==3,"ready diagnostic skips GDI bootstrap without hiding child subtree");
  MapGeometryRenderer.Active.SuppressGdiSelfRender=false;

 }
}
namespace TaleWorlds.Library{
 public struct Vec2{public float x,y;public Vec2(float x,float y){this.x=x;this.y=y;}public static Vec2 Forward=>new Vec2(0,1);public Vec2 Normalized()=>this;public Vec2 LeftVec()=>this;public float AngleBetween(Vec2 v)=>0;public static Vec2 operator -(Vec2 a)=>new Vec2(-a.x,-a.y);public static Vec2 operator -(Vec2 a,Vec2 b)=>new Vec2(a.x-b.x,a.y-b.y);public static Vec2 operator *(Vec2 a,float f)=>new Vec2(a.x*f,a.y*f);}
 public static class MathF{public static float Max(float a,float b)=>Math.Max(a,b);}public static class Debug{public static void Print(string s){}}
 public class Position{public Vec2 AsVec2=>new Vec2(0,0);}
}
namespace TaleWorlds.TwoDimension{public class Texture{public bool IsValid{get;set;}=true;}public class Material{public Texture Texture;}public struct Rect{public void CalculateVisualMatrixFrame(){}}public struct Image{public float Scale;public Rect Rectangle;}public class TwoDimensionContext{public int Count;public void DrawImage(Material m,Image i){Count++;}}public class TwoDimensionDrawContext{public int Count;public void Draw(Material m,in Image i){Count++;}}}
namespace TaleWorlds.GauntletUI{public enum HorizontalAlignment{Left,Center,Right} public enum VerticalAlignment{Top,Center,Bottom} public enum SizePolicy{Fixed}public class UIContext{}public class TextureProvider{public int Reads;public virtual TaleWorlds.TwoDimension.Texture GetTextureForRender(TaleWorlds.TwoDimension.TwoDimensionContext c){Reads++;return new TaleWorlds.TwoDimension.Texture();}}}
namespace TaleWorlds.GauntletUI.BaseTypes{public class TextureWidget{
 public TextureWidget(TaleWorlds.GauntletUI.UIContext c){}public string TextureProviderName;public TaleWorlds.GauntletUI.TextureProvider TextureProvider;public TaleWorlds.TwoDimension.Texture Texture{get;protected set;}public float SuggestedWidth=400,SuggestedHeight=400;public TaleWorlds.GauntletUI.SizePolicy WidthSizePolicy,HeightSizePolicy;public bool IsEnabled,IsVisible=true,DoNotAcceptEvents,CircularClipEnabled;public float CircularClipRadius,CircularClipSmoothingRadius,MarginLeft,MarginRight,MarginTop,MarginBottom;public TaleWorlds.GauntletUI.HorizontalAlignment HorizontalAlignment;public TaleWorlds.GauntletUI.VerticalAlignment VerticalAlignment;public readonly System.Collections.Generic.List<TextureWidget> Children=new System.Collections.Generic.List<TextureWidget>();protected float _scaleToUse=1;public int BaseRenderCalls;
 protected virtual void OnRender(TaleWorlds.TwoDimension.TwoDimensionContext c,TaleWorlds.TwoDimension.TwoDimensionDrawContext d){BaseRenderCalls++;}protected virtual void OnUpdate(float dt){}public void Render(TaleWorlds.TwoDimension.TwoDimensionContext c,TaleWorlds.TwoDimension.TwoDimensionDrawContext d){if(!IsVisible)return;OnRender(c,d);foreach(var child in Children)child.Render(c,d);}}}
namespace BattleMiniMap.Config{public class BattleMiniMapConfig{static BattleMiniMapConfig c=new BattleMiniMapConfig();public static BattleMiniMapConfig Get()=>c;public bool FollowMode,EnableMapGeometry=true;public int WidgetWidth=400,PositionX,PositionY;public TaleWorlds.GauntletUI.HorizontalAlignment HorizontalAlignment;public TaleWorlds.GauntletUI.VerticalAlignment VerticalAlignment;public float GetFollowModeScale()=>.5f;}}
namespace BattleMiniMap.View.MapTerrain{public class Map{public bool IsValid=true;public int BitmapWidth=500,BitmapHeight=500;public TaleWorlds.Library.Vec2 MapBoundMin=>new TaleWorlds.Library.Vec2(0,0);public TaleWorlds.Library.Vec2 MapBoundMax=>new TaleWorlds.Library.Vec2(500,500);public TaleWorlds.Library.Vec2 MapToWidget(TaleWorlds.Library.Vec2 p)=>p;public TaleWorlds.Library.Vec2 WorldToMapF(TaleWorlds.Library.Vec2 p)=>p;}public static class MiniMap{public static Map Instance=new Map();}}
namespace BattleMiniMap.Widgets{public static class Utility{
 public static TaleWorlds.TwoDimension.Material CreateMaterial2(TaleWorlds.TwoDimension.TwoDimensionDrawContext d,TaleWorlds.GauntletUI.BaseTypes.TextureWidget w)=>new TaleWorlds.TwoDimension.Material();
 public static TaleWorlds.Library.Vec2 GetSize(TaleWorlds.GauntletUI.BaseTypes.TextureWidget w)=>new TaleWorlds.Library.Vec2(w.SuggestedWidth,w.SuggestedHeight);
 public static TaleWorlds.TwoDimension.Image CreateDrawObject2D(TaleWorlds.GauntletUI.BaseTypes.TextureWidget w,float x,float y,float width,float height)=>new TaleWorlds.TwoDimension.Image();
 public static TaleWorlds.TwoDimension.Image CreateDrawObject2D(TaleWorlds.GauntletUI.BaseTypes.TextureWidget w,TaleWorlds.Library.Vec2 o,float width,float height,TaleWorlds.Library.Vec2 p,float rotation)=>new TaleWorlds.TwoDimension.Image();}}
namespace TaleWorlds.MountAndBlade{public class MissionState{public static MissionState Current=new MissionState();public T GetListenerOfType<T>() where T:new()=>new T();}}
namespace TaleWorlds.MountAndBlade.View.Screens{public class MissionScreen{public Camera CombatCamera=new Camera();}public class Camera{public TaleWorlds.Library.Position Position=new TaleWorlds.Library.Position(),Direction=new TaleWorlds.Library.Position();}}
namespace BattleMiniMap.View.MapGeometry{internal class MapGeometryRenderer{internal static MapGeometryRenderer Active;public TaleWorlds.TwoDimension.Texture Texture;public bool SuppressGdiSelfRender;}}

namespace TaleWorlds.Engine{
 public class Shader{public bool IsValid=true;public string Name;public static Shader Resolved;public static string Requested;public static Shader GetFromResource(string name){Requested=name;return Resolved;}}
 public class Material{Shader assigned;public Shader OverrideReadback;public int SetCalls;public void SetShader(Shader shader){assigned=shader;SetCalls++;}public Shader GetShader()=>OverrideReadback??assigned;}
}

namespace BattleMiniMap.View.Background.Map { public class BattleMiniMap_MiniMapTextureProvider {} }
