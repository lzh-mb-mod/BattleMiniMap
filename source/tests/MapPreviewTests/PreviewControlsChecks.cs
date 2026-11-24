using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;
using Vec2=TaleWorlds.Library.Vec2;

static class PreviewControlsChecks
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);++checks;}
    static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    public static void Run()
    {
        var config=BattleMiniMapConfig.Get();config.ShowGeometryPreview=true;config.UseLiveScenePreview=true;config.UseCollisionScenePreview=false;
        var preview=new MapGeometryPreviewRenderer(new Scene());preview.Update(null);
        var ui=new UIContext();var widget=new BattleMiniMap_GeometryPreviewWidget(ui);
        widget.GlobalPosition=new Vector2(17,29);
        foreach(var scale in new[]{.5f,1f,1.5f,2f})
        foreach(var size in new[]{new Vector2(200,150),new Vector2(640,480),new Vector2(960,640),new Vector2(960,200),new Vector2(40,80)})
        {
            widget.Scale=scale;widget.Size=size*scale;
            var context=new TwoDimensionContext();Invoke(widget,"OnRender",context,new TwoDimensionDrawContext());
            var layout=new MapPreviewLayout(widget.Size.X,widget.Size.Y,scale);
            Check(context.Images.Count==(layout.Height>0?1:0),"Actual OnRender skips zero-sized image viewport");
            if(context.Images.Count>0){var q=context.Images[0];Check(q.Y==64*scale && q.Y+q.Height<=(size.Y-60)*scale && q.Width==size.X*scale,"Actual drawn image excludes header/footer at every size and scale");}
            Invoke(widget,"OnUpdate",0f);preview.Update(null);
            if(layout.Height>0)Check(Math.Abs(TableauView.Last.NativeView.Camera.Aspect-layout.Width/layout.Height)<.0001f,"Production SetAspect uses measured draw viewport");
        }
        widget.Scale=1;widget.Size=new Vector2(960,200);
        Invoke(widget,"OnUpdate",0f);preview.Update(null);
        Check(Math.Abs(TableauView.Last.NativeView.Camera.Aspect-960f/76)<.0001f,"960x200 actual Widget delivers 960/76 aspect to final camera");
        foreach(var invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            preview.SetAspect(invalid);preview.Update(null);
            Check(Math.Abs(TableauView.Last.NativeView.Camera.Aspect-960f/76)<.0001f,"Invalid aspect cannot replace valid measured aspect: "+invalid);
        }
        preview.SetAspect(.01f);preview.Update(null);
        Check(TableauView.Last.NativeView.Camera.Aspect==.01f,"Finite positive narrow aspect is accepted");
        widget.Scale=1;widget.Size=new Vector2(640,480);widget.IsPressed=true;
        preview.Orbit.Orbit(13,7); // A control-area double click must not reset an already changed pose.
        foreach(var y in new[]{10f,40f,430f,475f})
        {
            ui.EventManager.MousePosition=widget.GlobalPosition+new Vector2(20,y);
            var yaw=preview.Orbit.Yaw;var distance=preview.Orbit.Distance;var target=preview.Orbit.Target;
            Invoke(widget,"OnMousePressed");ui.EventManager.MousePosition+=new Vector2(10,1);Invoke(widget,"OnUpdate",0f);Invoke(widget,"OnMouseReleased",true);
            Invoke(widget,"OnMouseAlternatePressed");ui.EventManager.MousePosition+=new Vector2(10,1);Invoke(widget,"OnUpdate",0f);Invoke(widget,"OnMouseAlternateReleased",true);
            Check(!(bool)widget.GetType().GetMethod("OnPreviewMouseScroll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(widget,null),"Header/footer parent preview declines wheel candidate so child scroll panel may receive it");
            ui.EventManager.DeltaMouseScroll=1;Invoke(widget,"OnMouseScroll");widget.Fire("DoubleClick");
            Check(preview.Orbit.Yaw==yaw && preview.Orbit.Distance==distance && preview.Orbit.Target.X==target.X && preview.Orbit.Target.Y==target.Y,"Controls reject orbit/pan/wheel/double-click transforms");
        }
        ui.EventManager.MousePosition=widget.GlobalPosition+new Vector2(50,100);
        Check((bool)widget.GetType().GetMethod("OnPreviewMouseScroll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(widget,null),"Image viewport remains a wheel event candidate");
        var beforeDragYaw=preview.Orbit.Yaw;var beforeDragPitch=preview.Orbit.Pitch;
        Invoke(widget,"OnMousePressed");
        ui.EventManager.MousePosition=widget.GlobalPosition+new Vector2(70,470);Invoke(widget,"OnUpdate",0f);Invoke(widget,"OnMouseReleased",true);
        var stopped=preview.Orbit.Yaw;ui.EventManager.MousePosition+=new Vector2(20,0);Invoke(widget,"OnUpdate",0f);
        Check(preview.Orbit.Yaw==stopped && stopped<beforeDragYaw && preview.Orbit.Pitch<beforeDragPitch,"Both orbit drag axes reverse and release outside image still ends capture");
        ui.EventManager.MousePosition=widget.GlobalPosition+new Vector2(50,100);Invoke(widget,"OnMouseAlternatePressed");
        var expected=new MapGeometryPreviewCamera();
        // Compare to a clone of the same pose using the production pan denominator.
        expected.Orbit((preview.Orbit.Yaw-expected.Yaw)/.006f,(preview.Orbit.Pitch-expected.Pitch)/.006f);
        expected.Pan(-10,-5,480-64-60);
        ui.EventManager.MousePosition+=new Vector2(10,5);Invoke(widget,"OnMouseAlternateReleased",true);
        Check(Math.Abs(preview.Orbit.Target.X-expected.Target.X)<.0001f && Math.Abs(preview.Orbit.Target.Z-expected.Target.Z)<.0001f,"Both pan drag axes reverse while retaining image height denominator");
        var old=TableauView.Last;preview.Orbit.Zoom(1);var eye=preview.Orbit.Eye;var at=preview.Orbit.Target;
        Check(preview.SwitchRuntimeSource() && config.UseCollisionScenePreview && preview.Texture==null && preview.Live==null && !old.NativeView.Enabled && !old.NativeView.Continuous,"Explicit switch detaches stale texture/collector and retires target immediately");
        var gets=old.ViewGets;old.Paint();Check(old.ViewGets==gets,"Source-switched callback cannot touch native view");
        preview.Update(null);Check(preview.Available && preview.Live.IsCollision && preview.Orbit.Eye.X==eye.X && preview.Orbit.Eye.Y==eye.Y && preview.Orbit.Eye.Z==eye.Z && preview.Orbit.Target.X==at.X && preview.Orbit.Target.Y==at.Y && preview.Orbit.Target.Z==at.Z,"Switch rebuilds selected source preserving orbit and pan");
        preview.Update(null);Check(old.IsReleased,"Source switch releases old target after deferral");
        var failed=TableauView.Last;failed.NativeView.Throw=true;failed.Paint();preview.Update(null);
        var created=Scene.Created;config.UseCollisionScenePreview=false;preview.Update(null);Check(Scene.Created==created && preview.Problem==MapPreviewProblem.RenderingFailed,"Ordinary updates and config changes cannot auto retry failure");
        Check(preview.SwitchRuntimeSource() && preview.Problem==MapPreviewProblem.None,"Explicit source action clears failed state");preview.Update(null);Check(preview.Available,"Explicit source action retries failed preview");
        config.UseLiveScenePreview=false;var sourceFlag=config.UseCollisionScenePreview;Check(!preview.SwitchRuntimeSource() && config.UseCollisionScenePreview==sourceFlag,"Cache mode cannot claim runtime switch");
        config.ShowGeometryPreview=false;Check(!preview.SwitchRuntimeSource(),"Closed panel cannot switch source");preview.Dispose();config.UseCollisionScenePreview=false;
        CheckPrefab();
        Console.WriteLine("PASS: "+checks+" preview controls checks through production Widget/renderer with fake UI/native boundary");
    }
    static void CheckPrefab()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../BattleMiniMap/Modules/BattleMiniMap"));
        var prefab=XDocument.Load(Path.Combine(root,"GUI/Prefabs/BattleMiniMapView.xml"));
        var children=prefab.Descendants("BattleMiniMap_GeometryPreviewWidget").Single().Element("Children");
        var footer=children.Elements("Widget").Single(e=>(string)e.Attribute("Id")=="PreviewFooter");
        Check((float)footer.Attribute("SuggestedHeight")==MapPreviewLayout.FooterHeight && (string)footer.Attribute("VerticalAlignment")=="Bottom","Scrollable footer height matches production viewport reservation");
        var scroll=footer.Element("Children").Element("ScrollablePanel");
        Check((string)scroll.Attribute("HeightSizePolicy")=="StretchToParent" && (string)scroll.Attribute("WidthSizePolicy")=="StretchToParent","Scroll viewport follows footer bounds at narrow widths");
        XElement Resolve(XElement origin,string path)
        {
            foreach(var part in path.Split('\\'))
                origin=part==".." ? origin.Parent.Parent : origin.Element("Children").Elements().Single(e=>(string)e.Attribute("Id")==part);
            return origin;
        }
        var clip=Resolve(scroll,(string)scroll.Attribute("ClipRect"));
        var inner=Resolve(scroll,(string)scroll.Attribute("InnerPanel"));
        var bar=Resolve(scroll,(string)scroll.Attribute("VerticalScrollbar"));
        Check((string)clip.Attribute("ClipContents")=="true" && inner.Parent.Parent==clip,"Parsed scroll paths connect clipped viewport and content");
        Check(inner.Name=="ListPanel" && (string)inner.Attribute("HeightSizePolicy")=="CoverChildren" && (string)inner.Attribute("WidthSizePolicy")=="StretchToParent" && (string)inner.Attribute("LayoutImp.LayoutMethod")=="VerticalTopToBottom","Status content expands vertically while following available width");
        var rows=inner.Element("Children").Elements("TextWidget").ToArray();
        Check(rows.Length==2 && rows.Select(e=>(string)e.Attribute("Text")).SequenceEqual(new[]{"@GeometryPreviewStatus","@GeometryPreviewDetails"}),"Both complete status fields are in scrollable content");
        Check(rows.All(e=>(string)e.Attribute("HeightSizePolicy")=="CoverChildren" && (string)e.Attribute("WidthSizePolicy")=="StretchToParent" && (string)e.Attribute("CanBreakWords")=="true" && e.Attribute("SuggestedHeight")==null),"Status rows wrap and grow instead of clipping to fixed 26px lines");
        Check(bar.Name=="ScrollbarWidget" && Resolve(bar,(string)bar.Attribute("Handle"))!=null,"Scrollable footer exposes connected draggable scrollbar handle");
        var switchButton=children.Elements("ButtonWidget").Single(e=>(string)e.Attribute("Command.Click")=="SwitchGeometryPreviewSource");
        var closeButton=children.Elements("ButtonWidget").Single(e=>(string)e.Attribute("Command.Click")=="CloseGeometryPreview");
        Check((float)switchButton.Attribute("SuggestedWidth")+(float)closeButton.Attribute("SuggestedWidth")<=200 && (string)switchButton.Attribute("IsVisible")=="@GeometryPreviewCanSwitchSource","Parsed header buttons fit minimum panel and runtime visibility binding");
        var backgrounds=children.Elements("Widget").Where(e=>(string)e.Attribute("Sprite")=="BlankWhiteSquare").ToArray();
        Check(backgrounds.Length==2 && backgrounds.All(e=>(string)e.Attribute("Color")=="#121820FF") && backgrounds.Any(e=>(string)e.Attribute("VerticalAlignment")=="Bottom" && (float)e.Attribute("SuggestedHeight")==MapPreviewLayout.FooterHeight),"Parsed prefab supplies opaque contrasting header/footer backgrounds");
        foreach(var file in new[]{"module_strings.xml","Languages/std_BattleMiniMap.xml","Languages/CNs/BattleMiniMap-zho-CN.xml"})
        {
            var strings=XDocument.Load(Path.Combine(root,"ModuleData",file)).Descendants("string").ToDictionary(e=>(string)e.Attribute("id"),e=>(string)e.Attribute("text"));
            var prefix=file=="module_strings.xml"?"str_battle_mini_map_":"BattleMiniMap_";
            foreach(var key in new[]{"live_preview_summary","live_preview_details","preview_switch_render","preview_switch_collision"})
                Check(strings.TryGetValue(prefix+key,out var text) && !string.IsNullOrWhiteSpace(text) && !text.Contains('\n'),"Localized independent row/action exists without newline dependency: "+file+" "+key);
        }
    }

}
namespace TaleWorlds.GauntletUI
{
    public enum SizePolicy { Fixed }
    public enum HorizontalAlignment { Center }
    public enum VerticalAlignment { Center }
    public class EventManager { public Vector2 MousePosition;public float DeltaMouseScroll; }
    public class UIContext { public TwoDimensionContext TwoDimensionContext=new TwoDimensionContext();public EventManager EventManager=new EventManager(); }
}
namespace TaleWorlds.GauntletUI.BaseTypes
{
    public class Widget
    {
        public Widget(UIContext context){Context=context;}
        public UIContext Context; public EventManager EventManager=>Context.EventManager;
        public Vector2 Size,GlobalPosition;public float Scale=1;protected float _scaleToUse=>Scale;
        public bool IsEnabled=true,IsVisible=true,IsPressed,AcceptDrag;public float SuggestedWidth,SuggestedHeight;
        public SizePolicy WidthSizePolicy,HeightSizePolicy;public HorizontalAlignment HorizontalAlignment;public VerticalAlignment VerticalAlignment;
        public int ChildCount=>0;public Widget GetChild(int i)=>null;public bool IsRecursivelyVisible()=>IsVisible;
        public bool IsPointInsideMeasuredArea(Vector2 p)=>true;
        public event Action<Widget,string,object[]> EventFire;public void Fire(string name)=>EventFire?.Invoke(this,name,Array.Empty<object>());
        protected virtual void OnUpdate(float dt){} protected virtual void OnRender(TwoDimensionContext c,TwoDimensionDrawContext d){}
    }
    public class ButtonWidget:Widget
    {
        public ButtonWidget(UIContext c):base(c){}
        protected virtual void OnMousePressed(){} protected virtual void OnMouseAlternatePressed(){}
        protected virtual void OnMouseReleased(bool input){} protected virtual void OnMouseAlternateReleased(bool input){}
        protected virtual bool OnPreviewMouseScroll()=>false;protected virtual void HandleClick(){}protected virtual void OnMouseScroll(){}
    }
}
namespace TaleWorlds.TwoDimension
{
    public class TwoDimensionContext
    {
        public float Width=1920,Height=1080;public readonly List<ImageDrawObject> Images=new List<ImageDrawObject>();
        public void DrawImage(SimpleMaterial m,ImageDrawObject q,int layer)=>Images.Add(q);
    }
    public class TwoDimensionDrawContext{}
    public class SimpleMaterial { public Texture Texture;public float AlphaFactor; }
    public class Rectangle { public void CalculateVisualMatrixFrame(){} }
    public class ImageDrawObject { public float X,Y,Width,Height;public Rectangle Rectangle=new Rectangle(); }
}
namespace BattleMiniMap.Widgets
{
    public static class Utility
    {
        public static Vec2 GetSize(Widget w)=>new Vec2(w.Size.X,w.Size.Y);
        public static Vec2 GetGlobalPosition(Widget w)=>new Vec2(w.GlobalPosition.X,w.GlobalPosition.Y);
        public static Vec2 GetMousePosition(EventManager e)=>new Vec2(e.MousePosition.X,e.MousePosition.Y);
        public static SimpleMaterial CreateMaterial2(TwoDimensionDrawContext c,Widget w)=>new SimpleMaterial();
        public static ImageDrawObject CreateDrawObject2D(Widget w,float x,float y,float width,float height)=>new ImageDrawObject{X=x,Y=y,Width=width,Height=height};
    }
}
