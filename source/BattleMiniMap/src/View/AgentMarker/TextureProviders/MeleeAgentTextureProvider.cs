using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.View.Image;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker.TextureProviders
{
    public class MeleeAgentTextureProvider : IAgentTextureProvider
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
            Draw(graphics, new Pen(Color.FromArgb(255, 50, 50, 50), 3));
            Fill(graphics, new SolidBrush(Color.FromArgb(255, 255, 255, 255)));
            return bitmap.CreateTexture();
        }

        private void Draw(Graphics graphics, Pen pen)
        {
            graphics.DrawRectangle(pen, 25, 25, 50, 50);
            graphics.DrawPolygon(pen, new Point[]
            {
                new Point(50, 0),
                new Point(33, 67),
                new Point(67, 67)
            });
        }
        private void Fill(Graphics graphics, Brush brush)
        {
            graphics.FillRectangle(brush, 25, 25, 50, 50);
            graphics.FillPolygon(brush, new Point[]
            {
                new Point(50, 0),
                new Point(33, 67),
                new Point(67, 67)
            });
        }
    }
}
