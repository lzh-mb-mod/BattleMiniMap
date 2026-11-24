namespace BattleMiniMap.View.Interaction
{
    internal enum MapRightRoute { Normal,Scene,Preview }
    // Origin owns the entire gesture, including a cursor warp on button release.
    internal sealed class MapRightInputPolicy
    {
        private bool _down,_preview;
        public MapRightRoute Update(bool pressed,bool down,bool released,bool overPreview)
        {
            if(!pressed&&!down&&!released){Reset();return MapRightRoute.Normal;}
            if(pressed&&!_down){_preview=overPreview;_down=true;}
            else if(down&&!_down){_preview=false;_down=true;} // Layer appeared mid-gesture.
            if(released)_down=false;
            return _preview?MapRightRoute.Preview:MapRightRoute.Scene;
        }
        public MapRightRoute Peek(bool rightActive)=>rightActive?(_preview?MapRightRoute.Preview:MapRightRoute.Scene):MapRightRoute.Normal;
        public void Reset(){_down=false;_preview=false;}
    }
}
