using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;
namespace BattleMiniMap.View.MapGeometry
{
    public sealed class BattleMiniMap_GpuDiagnosticWidget : BrushWidget
    {
        public BattleMiniMap_GpuDiagnosticWidget(UIContext context):base(context)
        {
            WidthSizePolicy=HeightSizePolicy=SizePolicy.Fixed;SuggestedWidth=SuggestedHeight=256;
            HorizontalAlignment=HorizontalAlignment.Right;VerticalAlignment=VerticalAlignment.Top;MarginRight=20;MarginTop=44;
        }
        private Texture _previous;
        protected override void OnRender(TwoDimensionContext context,TwoDimensionDrawContext drawContext)
        {
            var texture=MapGpuDiagnosticRenderer.Active?.Texture;if(texture==null || !texture.IsValid)return;
            if(!ReferenceEquals(_previous,texture)){_previous=texture;TaleWorlds.Library.Debug.Print("BattleMiniMap GPU diagnostic UI: valid="+texture.IsValid);}
            var material=Widgets.Utility.CreateMaterial2(drawContext,this);material.Texture=texture;material.AlphaFactor=1;
            var quad=Widgets.Utility.CreateDrawObject2D(this,0,0,SuggestedWidth*_scaleToUse,SuggestedHeight*_scaleToUse);quad.Rectangle.CalculateVisualMatrixFrame();context.DrawImage(material,quad,10);
        }
    }
}
