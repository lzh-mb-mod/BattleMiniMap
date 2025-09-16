using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    public class HeroAgentTextureProvider : IAgentTextureProvider
    {
        public const float HeroTextureScale = 4;
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
            var bitmap = new Bitmap(200, 200, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            //Draw(graphics, new Pen(Color.FromArgb(255, 50, 50, 50), 3));
            Fill(graphics, new SolidBrush(Color.FromArgb(255, 255, 255, 255)));
            return bitmap;
        }

        private Texture CreateTexture()
        {
            return GetBitmap().CreateTexture();
        }

        //private void Draw(Graphics graphics, Pen pen)
        //{
        //    graphics.DrawEllipse(pen, 25, 25, 50, 50);
        //    graphics.DrawPolygon(pen, new Point[]
        //    {
        //        new Point(50, 0),
        //        new Point(33, 67),
        //        new Point(67, 67)
        //    });
        //}

        private void Fill(Graphics graphics, Brush brush)
        {
            // 128 to 32
            graphics.FillEllipse(brush, 84, 84, 32, 32);
            var graphicsPath = new GraphicsPath();
            graphicsPath.AddArc(52, 68, 64, 64, -60, 69);
            graphicsPath.AddLine(116, 100, 84, 100);
            graphicsPath.AddArc(84, 68, 64, 64, 180, 60);
            graphicsPath.CloseFigure();
            graphics.FillPath(brush, graphicsPath);
        }
    }
}
