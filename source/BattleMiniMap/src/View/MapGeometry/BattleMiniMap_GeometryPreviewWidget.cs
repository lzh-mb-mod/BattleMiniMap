extern alias GameVectors;
using UiVector2=GameVectors::System.Numerics.Vector2;
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
            renderer?.SetAspect(SuggestedWidth/Math.Max(1,SuggestedHeight-32));
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
            else if (_drag==2) renderer?.Orbit.Pan(delta.x,delta.y,SuggestedHeight-32);
        }
        protected override void OnMousePressed() { base.OnMousePressed(); _drag=1; _dragDistance=0; _previous=Widgets.Utility.GetMousePosition(EventManager); }
        protected override void OnMouseAlternatePressed() { base.OnMouseAlternatePressed(); _drag=2; _previous=Widgets.Utility.GetMousePosition(EventManager); }
        protected override void OnMouseReleased(bool isFromInput) { ApplyDrag(); base.OnMouseReleased(isFromInput); _drag=0; }
        protected override void OnMouseAlternateReleased(bool isFromInput) { ApplyDrag(); base.OnMouseAlternateReleased(isFromInput); _drag=0; }
        protected override bool OnPreviewMouseScroll() => true;
        protected override void HandleClick() { if (_dragDistance<5) base.HandleClick(); }
        protected override void OnMouseScroll()
        {
            base.OnMouseScroll();
            MapGeometryPreviewRenderer.Active?.Orbit.Zoom(Math.Sign(EventManager.DeltaMouseScroll));
        }
        private void OnEvent(Widget widget,string name,object[] args)
        { if (widget==this && name=="DoubleClick") MapGeometryPreviewRenderer.Active?.Orbit.Reset(); }
        protected override void OnRender(TwoDimensionContext context,TwoDimensionDrawContext drawContext)
        {
            var texture=MapGeometryPreviewRenderer.Active?.Texture;
            if (texture?.IsValid!=true) return;
            var material=Widgets.Utility.CreateMaterial2(drawContext,this); material.Texture=texture; material.AlphaFactor=1;
            var quad=Widgets.Utility.CreateDrawObject2D(this,0,32*_scaleToUse,Widgets.Utility.GetSize(this).x,Math.Max(1,Widgets.Utility.GetSize(this).y-32*_scaleToUse));
            quad.Rectangle.CalculateVisualMatrixFrame(); context.DrawImage(material,quad,10);
        }
    }
}
