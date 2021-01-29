using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMap_MiniMapTextureProvider : TextureProvider
    {

        public override Texture GetTexture(TwoDimensionContext twoDimensionContext, string name)
        {
            //return SimpleMiniMap.Instance?.MapTexture ?? new ImageRGBA(100, 100).CreateTexture();
            return MiniMap.Instance?.MapTexture ?? new ImageRGBA(100, 100).CreateTexture();
        }
    }
}