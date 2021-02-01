using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.Widgets
{
    public class Utility
    {
        public static DrawObject2D CreateDrawObject2D(float width, float height)
        {
            DrawObject2D polygonCoordinates = DrawObject2D.CreateTriangleTopologyMeshWithPolygonCoordinates(new List<Vector2>()
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, height),
                new Vector2(width, height),
                new Vector2(width, 0.0f)
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
            polygonCoordinates.Width = width;
            polygonCoordinates.Height = height;
            polygonCoordinates.MinU = 0.0f;
            polygonCoordinates.MaxU = 1f;
            polygonCoordinates.MinV = 0.0f;
            polygonCoordinates.MaxV = 1f;
            return polygonCoordinates;
        }

        public static SimpleMaterial CreateMaterial(TwoDimensionDrawContext drawContext, BrushWidget widget)
        {
            SimpleMaterial simpleMaterial = drawContext.CreateSimpleMaterial();
            Brush brush = widget.Brush;
            StyleLayer styleLayer;
            if (brush == null)
            {
                styleLayer = null;
            }
            else
            {
                Dictionary<string, StyleLayer>.ValueCollection layers = brush.GetStyleOrDefault(widget .CurrentState).Layers;
                styleLayer = layers?.FirstOrDefault();
            }
            simpleMaterial.OverlayEnabled = false;
            simpleMaterial.CircularMaskingEnabled = false;
            simpleMaterial.AlphaFactor = (styleLayer?.AlphaFactor ?? 1f) * widget .Brush.GlobalAlphaFactor * widget .Context.ContextAlpha;
            simpleMaterial.ColorFactor = (styleLayer?.ColorFactor ?? 1f) * widget.Brush.GlobalColorFactor;
            simpleMaterial.HueFactor = styleLayer?.HueFactor ?? 0.0f;
            simpleMaterial.SaturationFactor = styleLayer?.SaturationFactor ?? 0.0f;
            simpleMaterial.ValueFactor = styleLayer?.ValueFactor ?? 0.0f;
            simpleMaterial.Color = (styleLayer?.Color ?? Color.White) * widget.Brush.GlobalColor;
            return simpleMaterial;
        }

        public static Vec2 GetGlobalPosition(Widget widget)
        {
            return new Vec2(widget.GlobalPosition.X, widget.GlobalPosition.Y);
        }

        public static Vec2 GetSize(Widget widget)
        {
            return new Vec2(widget.Size.X, widget.Size.Y);
        }
    }
}
