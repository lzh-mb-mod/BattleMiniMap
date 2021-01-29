using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.Widgets
{
    public abstract class MapItemWidget : ImageWidget
    {
        protected DrawObject2D CachedMesh;

        public abstract Texture Texture { get; }

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
                Dictionary<string, StyleLayer>.ValueCollection layers = brush.GetStyleOrDefault(CurrentState).Layers;
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
            drawContext.Draw(GlobalPosition.X, GlobalPosition.Y, simpleMaterial, CachedMesh, Size.X, Size.Y);
        }

        protected virtual void UpdateDrawObject2D()
        {
            CachedMesh = CreateDrawObject2D();
        }

        protected DrawObject2D CreateDrawObject2D()
        {
            DrawObject2D polygonCoordinates = DrawObject2D.CreateTriangleTopologyMeshWithPolygonCoordinates(new List<Vector2>()
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, Size.Y),
                new Vector2(Size.X, Size.Y),
                new Vector2(Size.X, 0.0f)
            });
            polygonCoordinates.DrawObjectType = DrawObjectType.Quad;
            polygonCoordinates.TextureCoordinates[0] = 0.0f;
            polygonCoordinates.TextureCoordinates[1] = 0.0f;
            polygonCoordinates.TextureCoordinates[2] = 0.0f;
            polygonCoordinates.TextureCoordinates[3] = 1f;
            polygonCoordinates.TextureCoordinates[4] = 1f;
            polygonCoordinates.TextureCoordinates[5] = 1f;
            polygonCoordinates.TextureCoordinates[6] = 0.0f;
            polygonCoordinates.TextureCoordinates[7] = 0.0f;
            polygonCoordinates.TextureCoordinates[8] = 1f;
            polygonCoordinates.TextureCoordinates[9] = 1f;
            polygonCoordinates.TextureCoordinates[10] = 1f;
            polygonCoordinates.TextureCoordinates[11] = 0.0f;
            polygonCoordinates.Width = Size.X;
            polygonCoordinates.Height = Size.Y;
            polygonCoordinates.MinU = 0.0f;
            polygonCoordinates.MaxU = 1f;
            polygonCoordinates.MinV = 0.0f;
            polygonCoordinates.MaxV = 1f;
            return polygonCoordinates;
        }
    }
}
