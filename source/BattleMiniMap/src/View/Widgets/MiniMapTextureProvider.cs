using BattleMiniMap.View.Image;
using BattleMiniMap.View.Map;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Widgets
{
    public class MiniMapTextureProvider : TextureProvider
    {
        public override Texture GetTexture(TwoDimensionContext twoDimensionContext, string name)
        {
            return MiniMap.Instance?.MapTexture ?? new ImageRGBA(100, 100).CreateTexture();
        }
    }
}
