using System;
using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Background.Boundary
{
    public class BattleMiniMap_BoundaryTextureProvider : TextureProvider
    {
        private int _boundaryBitmapWidth;
        private Texture _texture;
        private static Texture _textureToBeReleased;

        public Texture Texture
        {
            get => _texture;
            private set
            {
                if (_texture == value)
                    return;
                TextureToBeReleased = _texture;
                _texture = value;
            }
        }

        public static Texture TextureToBeReleased
        {
            get => _textureToBeReleased;
            set
            {
                if (_textureToBeReleased == value)
                    return;
                (_textureToBeReleased?.PlatformTexture as EngineTexture)?.Texture.Release();
                _textureToBeReleased = value;
            }
        }

        public override Texture GetTexture(TwoDimensionContext twoDimensionContext, string name)
        {
            return Texture ??= CreateTexture();
        }

        public override void Clear()
        {
            base.Clear();
            Texture = null;
        }

        private Texture CreateTexture()
        {
            var miniMap = MiniMap.Instance;
            var widgetWidth = BattleMiniMapConfig.Get().WidgetWidth;
            var width = Math.Max(miniMap.BitmapWidth, widgetWidth);
            _boundaryBitmapWidth = width;
            var scale = (float)width / Math.Max(miniMap.BitmapWidth, 1);
            var height = (int) (scale * miniMap.BitmapHeight);
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            var widthOfOnePixel = (float)miniMap.BitmapWidth /
                                  (BattleMiniMapConfig.Get().WidgetWidth == 0
                                      ? 1
                                      : BattleMiniMapConfig.Get().WidgetWidth);
            DrawBoundaries(Mission.Current, graphics, widthOfOnePixel);
            return bitmap.CreateTexture();
        }

        private void DrawBoundaries(Mission mission, Graphics graphics, float widthOfOnePixel)
        {
            var miniMap = MiniMap.Instance;
            var scale = (float)_boundaryBitmapWidth / Math.Max(MiniMap.Instance.BitmapWidth, 1);
            foreach (var boundary in mission.Boundaries)
            {
                var previousPos = Vec2.Invalid;
                var firstPos = Vec2.Invalid;
                Vec2 previousPoint = Vec2.Invalid;
                var pen = new Pen(System.Drawing.Color.FromArgb(64, 179, 30, 9), 2 * widthOfOnePixel);
                foreach (var vec2 in boundary.Value)
                {
                    if (!previousPos.IsValid)
                    {
                        previousPos = vec2;
                        firstPos = vec2;
                        previousPoint = miniMap.WorldToMapF(previousPos);
                        continue;
                    }

                    var currentPoint = miniMap.WorldToMapF(vec2);
                    graphics.DrawLine(pen, (int)(previousPoint.X * scale), (int)(previousPoint.Y * scale), (int)(currentPoint.X * scale), (int)(currentPoint.Y * scale));
                    previousPos = vec2;
                    previousPoint = currentPoint;
                }

                var finalPoint = miniMap.WorldToMapF(firstPos);
                graphics.DrawLine(pen, (int)(previousPoint.X * scale), (int)(previousPoint.Y * scale), (int)(finalPoint.X * scale), (int)(finalPoint.Y * scale));
            }
        }
    }
}
