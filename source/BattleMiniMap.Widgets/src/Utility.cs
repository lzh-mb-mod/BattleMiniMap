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
        public static ImageDrawObject CreateDrawObject2D(Widget widget, float x, float y, float width, float height)
        {
            var rect = widget.AreaRect;
            rect.LocalPosition = new Vector2(widget.LocalPosition.X + x, widget.LocalPosition.Y + y);
            rect.LocalScale = new Vector2(width, height);
            rect.CalculateMatrixFrame(widget.ParentWidget == null ? widget.EventManager.AreaRectangle : widget.ParentWidget.AreaRect);
            return ImageDrawObject.Create(rect, new Vec2(0, 0), new Vec2(1, 1));
        }

        public static ImageDrawObject CreateDrawObject2D(Widget widget, Vec2 offset, float width, float height, Vec2 actualRotateCenter, float rotateAngleInRadians)
        {
            var rectangle = widget.AreaRect;
            rectangle.LocalPosition = new Vector2(widget.LocalPosition.X + offset.x, widget.LocalPosition.Y + offset.y);
            rectangle.LocalScale = new Vector2(width, height);
            rectangle.LocalPivot = new Vector2(actualRotateCenter.x, actualRotateCenter.y);
            rectangle.LocalRotation = rotateAngleInRadians * 180 / Mathf.PI;
            rectangle.CalculateMatrixFrame(widget.ParentWidget == null ? widget.EventManager.AreaRectangle : widget.ParentWidget.AreaRect);
            return ImageDrawObject.Create(in rectangle, new Vec2(0, 0), new Vec2(1, 1));
            //Vec2 topLeft = Rotate(new Vec2(0, 0) + offset, actualRotateCenter, rotateAngleInRadians);
            //Vec2 bottomLeft = Rotate(new Vec2(0, height) + offset, actualRotateCenter, rotateAngleInRadians);
            //Vec2 bottomRight = Rotate(new Vec2(width, height) + offset, actualRotateCenter, rotateAngleInRadians);
            //Vec2 topRight = Rotate(new Vec2(width, 0) + offset, actualRotateCenter, rotateAngleInRadians);
            //ImageDrawObject polygonCoordinates = ImageDrawObject.CreateTriangleTopologyMeshWithPolygonCoordinates(new List<Vector2>()
            //{
            //    new Vector2(topLeft.x, topLeft.y),
            //    new Vector2(bottomLeft.x, bottomLeft.y),
            //    new Vector2(bottomRight.x, bottomRight.y),
            //    new Vector2(topRight.x, topRight.y)
            //});
            //polygonCoordinates.TextureCoordinates[0] = 0.0f;
            //polygonCoordinates.TextureCoordinates[1] = 0.0f;
            //polygonCoordinates.TextureCoordinates[2] = 0.0f;
            //polygonCoordinates.TextureCoordinates[3] = 1f;
            //polygonCoordinates.TextureCoordinates[4] = 1f;
            //polygonCoordinates.TextureCoordinates[5] = 1f;
            //polygonCoordinates.TextureCoordinates[6] = 0.0f;
            //polygonCoordinates.TextureCoordinates[7] = 0.0f;
            //polygonCoordinates.TextureCoordinates[8] = 1f;
            //polygonCoordinates.TextureCoordinates[9] = 1f;
            //polygonCoordinates.TextureCoordinates[10] = 1f;
            //polygonCoordinates.TextureCoordinates[11] = 0.0f;
            //polygonCoordinates.Width = width;
            //polygonCoordinates.Height = height;
            //polygonCoordinates.MinU = 0.0f;
            //polygonCoordinates.MaxU = 1f;
            //polygonCoordinates.MinV = 0.0f;
            //polygonCoordinates.MaxV = 1f;
            //return polygonCoordinates;
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
                var layers = brush.GetStyleOrDefault(widget.CurrentState).GetLayers();
                styleLayer = layers?.FirstOrDefault();
            }
            simpleMaterial.OverlayEnabled = false;
            simpleMaterial.CircularMaskingEnabled = false;
            simpleMaterial.NinePatchParameters = SpriteNinePatchParameters.Empty;
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

        public static SimpleMaterial CreateMaterial2(TwoDimensionDrawContext drawContext, BrushWidget widget)
        {
            SimpleMaterial simpleMaterial = drawContext.CreateSimpleMaterial();
            StyleLayer[] layers = widget.ReadOnlyBrush.GetStyleOrDefault(widget.CurrentState).GetLayers();
            simpleMaterial.OverlayEnabled = false;
            simpleMaterial.CircularMaskingEnabled = false;
            simpleMaterial.NinePatchParameters = SpriteNinePatchParameters.Empty;
            if (layers != null && layers.Length != 0)
            {
                StyleLayer styleLayer = layers[0];
                simpleMaterial.AlphaFactor = styleLayer.AlphaFactor * widget.ReadOnlyBrush.GlobalAlphaFactor * widget.Context.ContextAlpha;
                simpleMaterial.ColorFactor = styleLayer.ColorFactor * widget.ReadOnlyBrush.GlobalColorFactor;
                simpleMaterial.HueFactor = styleLayer.HueFactor;
                simpleMaterial.SaturationFactor = styleLayer.SaturationFactor;
                simpleMaterial.ValueFactor = styleLayer.ValueFactor;
                simpleMaterial.Color = styleLayer.Color * widget.ReadOnlyBrush.GlobalColor;
            }
            else
            {
                simpleMaterial.AlphaFactor = widget.ReadOnlyBrush.GlobalAlphaFactor * widget.Context.ContextAlpha;
                simpleMaterial.ColorFactor = widget.ReadOnlyBrush.GlobalColorFactor;
                simpleMaterial.HueFactor = 0f;
                simpleMaterial.SaturationFactor = 0f;
                simpleMaterial.ValueFactor = 0f;
                simpleMaterial.Color = Color.White * widget.ReadOnlyBrush.GlobalColor;
            }
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

        public static Vec2 GetLocalPosition(Widget widget)
        {
            return new Vec2(widget.LocalPosition.X, widget.LocalPosition.Y);
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

        public static Vec2 GetMousePosition(EventManager eventManager)
        {
            var vector = eventManager.MousePosition;
            return new Vec2(vector.X, vector.Y);
        }
    }
}
