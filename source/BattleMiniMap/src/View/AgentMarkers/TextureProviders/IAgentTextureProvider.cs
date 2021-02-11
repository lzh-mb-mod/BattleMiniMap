using System.Drawing;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    public interface IAgentTextureProvider
    {
        Bitmap GetBitmap();
        Texture GetTexture();
    }
}
