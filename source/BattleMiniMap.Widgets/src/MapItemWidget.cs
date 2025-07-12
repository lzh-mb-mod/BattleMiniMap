using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.Widgets
{
    public abstract class MapItemWidget : BrushWidget
    {
        protected DrawObject2D CachedMesh;

        public abstract Texture Texture { get; }

        public int Layer { get; set; } = 1;

        protected MapItemWidget(UIContext context) : base(context)
        {
            WidthSizePolicy = SizePolicy.Fixed;
            HeightSizePolicy = SizePolicy.Fixed;
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);


            SimpleMaterial simpleMaterial = drawContext.CreateSimpleMaterial();
            Brush brush = Brush;
            StyleLayer styleLayer;
            if (brush == null)
            {
                styleLayer = null;
            }
            else
            {
                var layers = brush.GetStyleOrDefault(CurrentState).GetLayers();
                styleLayer = layers?.FirstOrDefault();
            }
            simpleMaterial.OverlayEnabled = false;
            simpleMaterial.CircularMaskingEnabled = false;
            simpleMaterial.Texture = Texture;
            simpleMaterial.AlphaFactor = (styleLayer?.AlphaFactor ?? 1f) * Brush.GlobalAlphaFactor * Context.ContextAlpha;
            simpleMaterial.ColorFactor = (styleLayer?.ColorFactor ?? 1f) * Brush.GlobalColorFactor;
            simpleMaterial.HueFactor = styleLayer?.HueFactor ?? 0.0f;
            simpleMaterial.SaturationFactor = styleLayer?.SaturationFactor ?? 0.0f;
            simpleMaterial.ValueFactor = styleLayer?.ValueFactor ?? 0.0f;
            simpleMaterial.Color = (styleLayer?.Color ?? Color.White) * Brush.GlobalColor;
            if (CachedMesh == null || Math.Abs(CachedMesh.Width - Size.X) > 0.01f && Math.Abs(CachedMesh.Height - Size.Y) > 0.01f)
            {
                UpdateDrawObject2D();
            }
            if (drawContext.CircularMaskEnabled)
            {
                simpleMaterial.CircularMaskingEnabled = true;
                simpleMaterial.CircularMaskingCenter = drawContext.CircularMaskCenter;
                simpleMaterial.CircularMaskingRadius = drawContext.CircularMaskRadius;
                simpleMaterial.CircularMaskingSmoothingRadius = drawContext.CircularMaskSmoothingRadius;
            }
            //drawContext.Draw(GlobalPosition.X, GlobalPosition.Y, simpleMaterial, CachedMesh, Size.X, Size.Y);
            twoDimensionContext.Draw(GlobalPosition.X, GlobalPosition.Y, simpleMaterial, CachedMesh, Layer);
        }

        protected virtual void UpdateDrawObject2D()
        {
            CachedMesh = CreateDrawObject2D();
        }

        protected DrawObject2D CreateDrawObject2D()
        {
            return Utility.CreateDrawObject2D(Size.X, Size.Y);
        }
    }
}
