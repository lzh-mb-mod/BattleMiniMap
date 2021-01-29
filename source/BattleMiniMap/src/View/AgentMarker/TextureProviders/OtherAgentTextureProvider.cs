using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.View.Image;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker.TextureProviders
{
    public class OtherAgentTextureProvider : IAgentTextureProvider
    {
        private Texture _texture;
        public Texture GetTexture()
        {
            return _texture ??= CreateTexture();
        }

        private Texture CreateTexture()
        {
            var bitmap = new Bitmap(50, 50, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            var brush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
            graphics.FillEllipse(brush, 16, 16, 18, 18);
            return bitmap.CreateTexture();
        }
    }
}
