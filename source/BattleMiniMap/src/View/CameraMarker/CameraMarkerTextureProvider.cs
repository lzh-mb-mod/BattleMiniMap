using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Imaging;
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
            var pen = new Pen(System.Drawing.Color.FromArgb(255, 30, 30, 30), 2);
            graphics.DrawLine(pen, new Point(0, 0), new Point(100, 100));
            return bitmap.CreateTexture(true);
        }
    }
}
