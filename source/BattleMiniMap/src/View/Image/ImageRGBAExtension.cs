using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Image
{
    public static class ImageRGBAExtension
    {
        public static Texture CreateTexture(this ImageRGBA image)
        {
            return new Texture(
                new EngineTexture(
                    TaleWorlds.Engine.Texture.CreateFromByteArray(image.Image, image.Width, image.Height)));
        }

        public static Texture CreateTexture(this Bitmap image, bool useHashAsName)
        {
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Bmp);
            var buffer = stream.GetBuffer();
            var platformTexture = new EngineTexture(TaleWorlds.Engine.Texture.CreateFromMemory(buffer));
            if (useHashAsName)
            {
                platformTexture.Texture.Name = platformTexture.GetHashCode().ToString();
            }
            return new Texture(platformTexture);
        }
    }
}