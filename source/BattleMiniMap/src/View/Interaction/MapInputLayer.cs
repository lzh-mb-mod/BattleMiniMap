#if INPUT_ROUTING_TESTS
using Vector2=System.Numerics.Vector2;
#else
extern alias GameVectors;
using Vector2=GameVectors::System.Numerics.Vector2;
#endif
using System;
using System.Globalization;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace BattleMiniMap.View.Interaction
{
    internal sealed class MapInputLayer : GauntletLayer
    {
        private readonly MapRightInputPolicy _right=new MapRightInputPolicy();
        public Func<Vector2,bool> PreviewHitTest {get;set;}
        private bool _baseHit,_previewHit,_layerHit,_tracePressed,_traceReleased;
        private int _traces;
        private Vector2 _position;
        private MapRightRoute _route;
        public MapInputLayer(string name,int order):base(name,order) { }
        private static bool Pressed=>TaleWorlds.InputSystem.Input.IsKeyPressed(InputKey.RightMouseButton);
        private static bool Down=>TaleWorlds.InputSystem.Input.IsKeyDown(InputKey.RightMouseButton);
        private static bool Released=>TaleWorlds.InputSystem.Input.IsKeyReleased(InputKey.RightMouseButton);
        public override bool HitTest()
        {
            _position=UIContext.EventManager.MousePosition;
            _baseHit=base.HitTest();_previewHit=PreviewHitTest?.Invoke(_position)==true;
            _route=_right.Update(Pressed,Down,Released,_previewHit);
            _layerHit=_route==MapRightRoute.Normal?_baseHit:_route==MapRightRoute.Preview;
            if(!_layerHit)UIContext.EventManager.HoveredWidget=null;
            return _layerHit;
        }
        public override bool HitTest(Vector2 position)
        {
            var route=_right.Peek(Pressed||Down||Released);
            return route==MapRightRoute.Normal?base.HitTest(position):route==MapRightRoute.Preview;
        }
        public void TraceSceneInput(bool sceneDown,bool sceneReleased,bool orderOpen,int mapWidth)
        {
            var pressed=Pressed;var released=Released;
            if(_traces<32&&((pressed&&!_tracePressed)||(released&&!_traceReleased)))
            {
                ++_traces;
                Debug.Print("BattleMiniMap mouse route: edge="+(released?"release":"press")+" owner="+_route+
                    " baseHit="+_baseHit+" layerHit="+_layerHit+" previewAtPoint="+_previewHit+
                    " sceneDown="+sceneDown+" sceneReleased="+sceneReleased+" orderOpenAtMapTick="+orderOpen+
                    " mapWidth="+mapWidth+" pointer="+_position.X.ToString("F1",CultureInfo.InvariantCulture)+","+_position.Y.ToString("F1",CultureInfo.InvariantCulture));
            }
            _tracePressed=pressed;_traceReleased=released;
        }
        public void ResetRouting(){_right.Reset();PreviewHitTest=null;}
    }
}
