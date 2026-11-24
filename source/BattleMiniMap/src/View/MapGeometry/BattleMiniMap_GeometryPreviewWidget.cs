#if PREVIEW_UI_TESTS
using UiVector2=System.Numerics.Vector2;
#else
extern alias GameVectors;
using UiVector2=GameVectors::System.Numerics.Vector2;
#endif
using System;
using BattleMiniMap.Config;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.MapGeometry
{
    // Only this visible panel accepts pointer events. Never requests keyboard focus.
    public sealed class BattleMiniMap_GeometryPreviewWidget : ButtonWidget
    {
        private int _drag;
        private float _dragDistance;
        private TaleWorlds.Library.Vec2 _previous;
        private MapPreviewLayout Viewport
        {
            get { var size=Widgets.Utility.GetSize(this); return new MapPreviewLayout(size.x,size.y,_scaleToUse); }
        }
        private bool PointerInViewport()
        {
            var p=Widgets.Utility.GetMousePosition(EventManager)-Widgets.Utility.GetGlobalPosition(this);
            return Viewport.Contains(p.x,p.y);
        }
        internal bool IsInteractiveAt(UiVector2 position) =>
            BattleMiniMapConfig.Get().ShowGeometryPreview && IsEnabled && IsRecursivelyVisible() && IsPointInsideMeasuredArea(position);
        internal static BattleMiniMap_GeometryPreviewWidget Find(Widget root)
        {
            if(root==null)return null;
            if(root is BattleMiniMap_GeometryPreviewWidget preview)return preview;
            for(var i=0;i<root.ChildCount;++i){var found=Find(root.GetChild(i));if(found!=null)return found;}
            return null;
        }
        public BattleMiniMap_GeometryPreviewWidget(UIContext context) : base(context)
        {
            WidthSizePolicy=HeightSizePolicy=SizePolicy.Fixed;
            SuggestedWidth=960; SuggestedHeight=640;
            AcceptDrag=false;
            HorizontalAlignment=HorizontalAlignment.Center; VerticalAlignment=VerticalAlignment.Center;
            EventFire+=OnEvent;
        }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            var scale=Math.Max(.01f,_scaleToUse);
            SuggestedWidth=Math.Max(200,Math.Min(960,Context.TwoDimensionContext.Width/scale-48));
            SuggestedHeight=Math.Max(150,Math.Min(640,Context.TwoDimensionContext.Height/scale-48));
            var renderer=MapGeometryPreviewRenderer.Active;
            var viewport=Viewport;
            if (viewport.Height>0) renderer?.SetAspect(viewport.Width/viewport.Height);
            if (!IsVisible || !BattleMiniMapConfig.Get().ShowGeometryPreview || !IsPressed) { _drag=0; return; }
            ApplyDrag();
        }
        private void ApplyDrag()
        {
            if (_drag==0) return;
            var position=Widgets.Utility.GetMousePosition(EventManager);
            var delta=(position-_previous)/Math.Max(.01f,_scaleToUse); _previous=position;
            _dragDistance+=Math.Abs(delta.x)+Math.Abs(delta.y);
            var renderer=MapGeometryPreviewRenderer.Active;
            if (_drag==1) renderer?.Orbit.Orbit(delta.x,delta.y);
            else if (_drag==2) renderer?.Orbit.Pan(delta.x,delta.y,Viewport.Height/Math.Max(.01f,_scaleToUse));
        }
        protected override void OnMousePressed() { base.OnMousePressed(); _drag=PointerInViewport()?1:0; _dragDistance=0; _previous=Widgets.Utility.GetMousePosition(EventManager); }
        protected override void OnMouseAlternatePressed() { base.OnMouseAlternatePressed(); _drag=PointerInViewport()?2:0; _previous=Widgets.Utility.GetMousePosition(EventManager); }
        protected override void OnMouseReleased(bool isFromInput) { ApplyDrag(); base.OnMouseReleased(isFromInput); _drag=0; }
        protected override void OnMouseAlternateReleased(bool isFromInput) { ApplyDrag(); base.OnMouseAlternateReleased(isFromInput); _drag=0; }
        protected override bool OnPreviewMouseScroll() => PointerInViewport();
        protected override void HandleClick() { if (_dragDistance<5) base.HandleClick(); }
        protected override void OnMouseScroll()
        {
            base.OnMouseScroll();
            if (PointerInViewport()) MapGeometryPreviewRenderer.Active?.Orbit.Zoom(Math.Sign(EventManager.DeltaMouseScroll));
        }
        private void OnEvent(Widget widget,string name,object[] args)
        { if (widget==this && name=="DoubleClick" && PointerInViewport()) MapGeometryPreviewRenderer.Active?.Orbit.Reset(); }
        protected override void OnRender(TwoDimensionContext context,TwoDimensionDrawContext drawContext)
        {
            var texture=MapGeometryPreviewRenderer.Active?.Texture;
            if (texture?.IsValid!=true) return;
            var viewport=Viewport;
            if (viewport.Width<=0 || viewport.Height<=0) return;
            var material=Widgets.Utility.CreateMaterial2(drawContext,this); material.Texture=texture; material.AlphaFactor=1;
            var quad=Widgets.Utility.CreateDrawObject2D(this,0,viewport.Top,viewport.Width,viewport.Height);
            quad.Rectangle.CalculateVisualMatrixFrame(); context.DrawImage(material,quad,10);
        }
    }
}
