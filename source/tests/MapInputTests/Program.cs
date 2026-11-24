using System;
using System.Numerics;
using System.Linq;
using BattleMiniMap.View.Interaction;
using TaleWorlds.InputSystem;

class Program
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);++checks;Console.WriteLine("PASS map input: "+message);}
    static bool preview;
    static bool InsidePreview(Vector2 p)=>preview&&p.X>=500&&p.X<=1460&&p.Y>=200&&p.Y<=840;
    static MapInputLayer Layer(int width)
    {
        var layer=new MapInputLayer("BattleMiniMap",12);layer.PreviewHitTest=InsidePreview;
        layer.BaseHit=p=>(p.X>=74&&p.X<=74+width&&p.Y>=69&&p.Y<=69+width)||InsidePreview(p);
        return layer;
    }
    static bool Frame(MapInputLayer layer,float x,float y,bool pressed=false,bool down=false,bool released=false)
    {
        Input.Pressed=pressed;Input.Down=down;Input.Released=released;layer.UIContext.EventManager.MousePosition=new Vector2(x,y);return layer.HitTest();
    }
    static void Main()
    {
        foreach(var width in new[]{800,950,1073})
        {
            preview=false;var layer=Layer(width);var centre=new Vector2(960,540);
            Check(layer.BaseHit(centre)==(width>=886),"Fixture reproduces width-dependent centre hit: "+width);
            Check(!Frame(layer,1600,900,true,true),"World-origin right press passes through, width "+width);
            Check(!Frame(layer,960,540,down:true),"RTS cursor warp into map cannot steal held right gesture, width "+width);
            Check(!Frame(layer,960,540,released:true),"Warped right release reaches lower layers, width "+width);
            Check(!layer.HitTest(centre),"Position query preserves world right ownership, width "+width);
            Frame(layer,960,540);
            Check(!Frame(layer,100,100,true,true)&&!Frame(layer,960,540,released:true),"Small map itself never owns a right gesture, width "+width);
            Check(Frame(layer,100,100)&&!Frame(layer,1600,900),"Normal left/wheel hit geometry is unchanged, width "+width);
        }
        preview=true;var p=Layer(1073);
        Check(Frame(p,600,300,true,true),"Visible preview owns right press within its measured area");
        Check(p.HitTest(new Vector2(1800,1000))&&Frame(p,1800,1000,down:true),"Preview pan retains capture outside panel");
        Check(Frame(p,1800,1000,released:true)&&Frame(p,1800,1000,released:true),"Outside release stays with preview, including repeated query in same frame");
        Check(!Frame(p,1800,1000),"Neutral frame clears completed preview gesture");
        Check(!Frame(p,1600,900,true,true)&&!Frame(p,600,300,down:true)&&!Frame(p,600,300,released:true),"World-origin gesture cannot be stolen when cursor moves into preview");
        Frame(p,1800,1000);Frame(p,600,300,true,true);preview=false;
        Check(Frame(p,960,540,down:true)&&Frame(p,960,540,released:true),"Hiding preview mid-pan does not leak the release to scene");
        Frame(p,1800,1000);preview=true;
        Check(!Frame(p,600,300,down:true)&&!Frame(p,600,300,released:true),"A new layer observation of an already-held button does not take ownership");
        Frame(p,1800,1000);Check(Frame(p,600,300,true,true),"Next new preview press can capture normally");
        p.ResetRouting();Input.Pressed=false;Check(!Frame(p,960,540,down:true)&&p.PreviewHitTest==null,"Reset removes preview binding and stale right capture");
        preview=false;var trace=Layer(1073);TaleWorlds.Library.Debug.Messages.Clear();
        for(var i=0;i<24;++i)
        {
            Frame(trace,1800,1000);trace.TraceSceneInput(false,false,true,1073);
            Frame(trace,1600,900,true,true);trace.TraceSceneInput(true,false,true,1073);
            Frame(trace,960,540,released:true);trace.TraceSceneInput(false,true,true,1073);
        }
        Check(TaleWorlds.Library.Debug.Messages.Count==32,"Pointer trace is bounded to 32 edge records per layer");
        Check(TaleWorlds.Library.Debug.Messages.Any(s=>s.Contains("edge=release owner=Scene baseHit=True layerHit=False")&&s.Contains("sceneReleased=True")),"Trace distinguishes intended pass-through, base rectangle hit and scene release");
        Check(!trace.IsFocusLayer&&trace.Order==12,"Input routing keeps original priority and never requests keyboard focus");
        Console.WriteLine("PASS: "+checks+" actual map input-layer checks at fake Gauntlet boundary; cursor warp, clipping-independent right routing and preview pan; no game input proof");
    }
}
namespace TaleWorlds.InputSystem
{
    enum InputKey { RightMouseButton }
    static class Input
    {
        public static bool Pressed,Down,Released;
        public static bool IsKeyPressed(InputKey key)=>Pressed;
        public static bool IsKeyDown(InputKey key)=>Down;
        public static bool IsKeyReleased(InputKey key)=>Released;
    }
}
namespace TaleWorlds.Engine.GauntletUI
{
    class Events {public Vector2 MousePosition;public object HoveredWidget;}
    class Context {public Events EventManager=new Events();}
    class GauntletLayer
    {
        public Context UIContext=new Context();public Func<Vector2,bool> BaseHit;public bool IsFocusLayer;public int Order;
        public GauntletLayer(string name,int order){Order=order;IsFocusLayer=false;}
        public virtual bool HitTest()=>HitBase(UIContext.EventManager.MousePosition);
        private bool HitBase(Vector2 position)=>BaseHit?.Invoke(position)==true;
        public virtual bool HitTest(Vector2 position)=>HitBase(position);
    }
}
namespace TaleWorlds.Library
{
    static class Debug {public static readonly System.Collections.Generic.List<string> Messages=new System.Collections.Generic.List<string>();public static void Print(string text)=>Messages.Add(text);}
}
