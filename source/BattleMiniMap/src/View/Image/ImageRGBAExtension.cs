
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
    }
}
