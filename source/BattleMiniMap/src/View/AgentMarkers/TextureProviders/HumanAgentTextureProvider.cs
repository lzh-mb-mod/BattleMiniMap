using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Imaging;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    public class HumanAgentTextureProvider : IAgentTextureProvider
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
            //Draw(graphics, new Pen(Color.FromArgb(255, 50, 50, 50), 3));
            Fill(graphics, new SolidBrush(Color.FromArgb(255, 255, 255, 255)));
            return bitmap;
        }

        private Texture CreateTexture()
        {
            return GetBitmap().CreateTexture(true);
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
            graphics.FillEllipse(brush, 9, 9, 32, 32);
        }
    }
}
