using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.View.Image;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker.TextureProviders
{
    public class DeadAgentTextureProvider : IAgentTextureProvider
    {
        private Texture _texture;
        public Texture GetTexture()
        {
            return _texture ??= CreateTexture();
        }

        private Texture CreateTexture()
        {
            var bitmap = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            var pen = new Pen(Color.FromArgb(255, 255, 255, 255), 20);
            graphics.DrawLine(pen, 25, 25, 75, 75);
            graphics.DrawLine(pen, 75, 25, 25, 75);
            return bitmap.CreateTexture();
        }
    }
}
