using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Imaging;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    public class DeadAgentTextureProvider : IAgentTextureProvider
    {
        private Bitmap _bitmap;
        private Texture _texture;

        public Bitmap GetBitmap()
        {
            return _bitmap ??= CreateBitMap();
        }
        public Texture GetTexture()
        {
            return _texture ??= CreateTexture();
        }

        private Bitmap CreateBitMap()
        {
            var bitmap = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            var pen = new Pen(Color.FromArgb(255, 255, 255, 255), 20);
            graphics.DrawLine(pen, 10, 10, 90, 90);
            graphics.DrawLine(pen, 90, 10, 10, 90);
            return bitmap;
        }

        private Texture CreateTexture()
        {
            return GetBitmap().CreateTexture();
        }
    }
}
