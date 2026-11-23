using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;
namespace BattleMiniMap.View.MapGeometry
{
    public sealed class BattleMiniMap_GpuDiagnosticWidget : BrushWidget
    {
        public BattleMiniMap_GpuDiagnosticWidget(UIContext context):base(context)
        {
            DoNotAcceptEvents=true;
            WidthSizePolicy=HeightSizePolicy=SizePolicy.Fixed;SuggestedWidth=SuggestedHeight=256;
            HorizontalAlignment=HorizontalAlignment.Right;VerticalAlignment=VerticalAlignment.Top;MarginRight=20;MarginTop=44;
        }
        private string _previousState;
        protected override void OnRender(TwoDimensionContext context,TwoDimensionDrawContext drawContext)
        {
            var renderer=MapGpuDiagnosticRenderer.Active;
            var mode=BattleMiniMap.Config.BattleMiniMapConfig.Get().MapGpuDiagnosticMode;
            if(mode==0 || renderer==null)return;
            var texture=renderer.Texture;var state=mode+":"+(texture!=null)+":"+(texture?.IsValid??false);
            if(_previousState!=state){_previousState=state;TaleWorlds.Library.Debug.Print("BattleMiniMap GPU diagnostic UI: mode="+mode+" target="+(texture!=null)+" valid="+(texture?.IsValid??false)+" cpuControl="+(renderer.ControlTexture?.IsValid??false));}
            if(renderer.ControlTexture?.IsValid==true)Draw(context,drawContext,renderer.ControlTexture,256,8);
            if(texture?.IsValid==true)Draw(context,drawContext,texture,256,256);
        }
        private void Draw(TwoDimensionContext context,TwoDimensionDrawContext drawContext,Texture texture,float width,float height)
        {
            var material=Widgets.Utility.CreateMaterial2(drawContext,this);material.Texture=texture;material.AlphaFactor=1;
            var offset=height==8?-12:0;
            var quad=Widgets.Utility.CreateDrawObject2D(this,0,offset,width*_scaleToUse,height*_scaleToUse);quad.Rectangle.CalculateVisualMatrixFrame();context.DrawImage(material,quad,10);
        }
    }
}
