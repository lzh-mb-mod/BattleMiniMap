using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.View.Image;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker.TextureProviders
{
    class HorseAgentTextureProvider : IAgentTextureProvider
    {
        private Texture _texture;
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

        private Texture CreateTexture()
        {
            var bitmap = new Bitmap(50, 50, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(bitmap);
            Fill(graphics, new SolidBrush(Color.FromArgb(255, 255, 255, 255)));
            return bitmap.CreateTexture();
        }

        private void Fill(Graphics graphics, Brush brush)
        {
            graphics.FillEllipse(brush, 5, 5, 40, 40);
            //graphics.FillPolygon(brush, new Point[]
            //{
            //    new Point(50, 0),
            //    new Point(33, 67),
            //    new Point(67, 67)
            //});
        }
    }
}
