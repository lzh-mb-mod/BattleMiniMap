using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Engine;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screen;
using TaleWorlds.TwoDimension;
using Texture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.CameraMarker
{
    public class CameraMarkerTextureProvider
    {
        private Texture _texture;
        public Texture GetTexture()
        {
            return _texture ??= CreateTexture();
        }

        public void Clear()
        {
            _texture = null;
        }

        private Texture CreateTexture()
        {
            var bitmap = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            var widthOfOnePixel = (float)MiniMap.Instance.BitmapWidth /
                                  (BattleMiniMapConfig.Get().WidgetWidth == 0
                                      ? 1
                                      : BattleMiniMapConfig.Get().WidgetWidth);
            var pen = new Pen(System.Drawing.Color.FromArgb(255, 30, 30, 30), widthOfOnePixel);
            graphics.DrawLine(pen, new Point(0, 0), new Point(100, 100));
            return bitmap.CreateTexture();
        }

        private Vec3 GetPositionFromScreen(Scene scene, Vec2 posOnScreen)
        {
            var missionScreen = MissionState.Current.Handler as MissionScreen;
            if (missionScreen == null)
                return Vec3.Invalid;
            missionScreen.ScreenPointToWorldRay(posOnScreen, out var rayBegin, out var rayEnd);
            if (scene.RayCastForClosestEntityOrTerrain(rayBegin, rayEnd, out var collisionDistance, 0.3f))
            {
                Vec3 vec3 = rayEnd - rayBegin;
                vec3.Normalize();
                return rayBegin + vec3 * collisionDistance;
            }

            return Vec3.Invalid;
        }
    }
}
