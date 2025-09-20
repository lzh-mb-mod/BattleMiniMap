using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Imaging;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    public class OtherAgentTextureProvider : IAgentTextureProvider
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
            var bitmap = new Bitmap(50, 50, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            var brush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
            graphics.FillEllipse(brush, 15, 15, 20, 20);
            return bitmap;
        }

        private Texture CreateTexture()
        {
            return GetBitmap().CreateTexture(true);
        }
    }
}
