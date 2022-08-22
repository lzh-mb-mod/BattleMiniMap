using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
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

        public static DrawObject2D CreateDrawObject2D(float width, float height, Vec2 offset ,Vec2 actualRotateCenter, float rotateAngleInRadians)
        {
            Vec2 topLeft = Rotate(new Vec2(0, 0) + offset, actualRotateCenter, rotateAngleInRadians);
            Vec2 bottomLeft = Rotate(new Vec2(0, height) + offset, actualRotateCenter, rotateAngleInRadians);
            Vec2 bottomRight = Rotate(new Vec2(width, height) + offset, actualRotateCenter, rotateAngleInRadians);
            Vec2 topRight = Rotate(new Vec2(width, 0) + offset, actualRotateCenter, rotateAngleInRadians);
            DrawObject2D polygonCoordinates = DrawObject2D.CreateTriangleTopologyMeshWithPolygonCoordinates(new List<Vector2>()
            {
                new Vector2(topLeft.x, topLeft.y),
                new Vector2(bottomLeft.x, bottomLeft.y),
                new Vector2(bottomRight.x, bottomRight.y),
                new Vector2(topRight.x, topRight.y)
            });
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
        private static Vec2 Rotate(Vec2 pos, Vec2 actualRotateCenter, float rotateAngleInRadians)
        {
            var vec = pos - actualRotateCenter;
            vec.RotateCCW(-rotateAngleInRadians);
            return vec + actualRotateCenter;
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
                Dictionary<string, StyleLayer>.ValueCollection layers = brush.GetStyleOrDefault(widget.CurrentState).Layers;
                styleLayer = layers?.FirstOrDefault();
            }
            simpleMaterial.OverlayEnabled = false;
            simpleMaterial.CircularMaskingEnabled = false;
            simpleMaterial.AlphaFactor = (styleLayer?.AlphaFactor ?? 1f) * widget.Brush.GlobalAlphaFactor * widget.Context.ContextAlpha;
            simpleMaterial.ColorFactor = (styleLayer?.ColorFactor ?? 1f) * widget.Brush.GlobalColorFactor;
            simpleMaterial.HueFactor = styleLayer?.HueFactor ?? 0.0f;
            simpleMaterial.SaturationFactor = styleLayer?.SaturationFactor ?? 0.0f;
            simpleMaterial.ValueFactor = styleLayer?.ValueFactor ?? 0.0f;
            simpleMaterial.Color = (styleLayer?.Color ?? Color.White) * widget.Brush.GlobalColor;
            if (drawContext.CircularMaskEnabled)
            {
                simpleMaterial.CircularMaskingEnabled = true;
                simpleMaterial.CircularMaskingCenter = drawContext.CircularMaskCenter;
                simpleMaterial.CircularMaskingRadius = drawContext.CircularMaskRadius;
                simpleMaterial.CircularMaskingSmoothingRadius = drawContext.CircularMaskSmoothingRadius;
            }
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

        public static void SetCircualMask(TwoDimensionDrawContext drawContext, Vec2 position, float radius,
            float smoothingRadius)
        {
            drawContext.SetCircualMask(new Vector2(position.x, position.y), radius, smoothingRadius);
        }
    }
}
