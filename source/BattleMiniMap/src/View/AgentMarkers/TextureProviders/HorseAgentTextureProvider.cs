using BattleMiniMap.View.Image;
using System.Drawing;
using System.Drawing.Imaging;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    class HorseAgentTextureProvider : IAgentTextureProvider
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

        //private Texture CreateTexture()
        //{
        //    var bitmap = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        //    var graphics = Graphics.FromImage(bitmap);
        //    var brush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
        //    Fill(graphics, brush);
        //    return bitmap.CreateTexture();
        //}

        //private void Fill(Graphics graphics, Brush brush)
        //{
        //    graphics.FillPolygon(brush, new Point[]
        //    {
        //        new Point(50, 0),
        //        new Point(13, 100),
        //        new Point(87, 100)
        //    });
        //}

        private Bitmap CreateBitMap()
        {
            var bitmap = new Bitmap(50, 50, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            Fill(graphics, new SolidBrush(Color.FromArgb(255, 255, 255, 255)));
            return bitmap;
        }

        private Texture CreateTexture()
        {
            return GetBitmap().CreateTexture();
        }

        private void Fill(Graphics graphics, Brush brush)
        {
            graphics.FillEllipse(brush, 0, 0, 50, 50);
            //graphics.FillPolygon(brush, new Point[]
            //{
            //    new Point(50, 0),
            //    new Point(33, 67),
            //    new Point(67, 67)
            //});
        }
    }
}
