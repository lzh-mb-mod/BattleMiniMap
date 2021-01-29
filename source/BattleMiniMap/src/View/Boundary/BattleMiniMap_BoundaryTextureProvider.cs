using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;
using Color = TaleWorlds.Library.Color;

namespace BattleMiniMap.View.Boundary
{
    public class BattleMiniMap_BoundaryTextureProvider : TextureProvider
    {
        private Texture _texture;

        public override Texture GetTexture(TwoDimensionContext twoDimensionContext, string name)
        {
            return _texture ??= CreateTexture();
        }

        public override void Clear()
        {
            base.Clear();
            _texture = null;
        }

        private Texture CreateTexture()
        {
            var miniMap = MiniMap.Instance;
            var bitmap = new Bitmap(miniMap.BitmapWidth, miniMap.BitmapHeight, PixelFormat.Format32bppArgb);
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
            foreach (var boundary in mission.Boundaries)
            {
                var previousPos = Vec2.Invalid;
                var firstPos = Vec2.Invalid;
                Point previousPoint = new Point();
                var pen = new Pen(System.Drawing.Color.FromArgb(255, 179, 30, 9), 2 * widthOfOnePixel);
                foreach (var vec2 in boundary.Value)
                {
                    if (!previousPos.IsValid)
                    {
                        previousPos = vec2;
                        firstPos = vec2;
                        previousPoint = miniMap.WorldToMap(previousPos);
                        continue;
                    }

                    var currentPoint = miniMap.WorldToMap(vec2);
                    graphics.DrawLine(pen, previousPoint, currentPoint);
                    previousPos = vec2;
                    previousPoint = currentPoint;
                }

                var finalPoint = miniMap.WorldToMap(firstPos);
                graphics.DrawLine(pen, previousPoint, finalPoint);
            }
        }
    }
}
