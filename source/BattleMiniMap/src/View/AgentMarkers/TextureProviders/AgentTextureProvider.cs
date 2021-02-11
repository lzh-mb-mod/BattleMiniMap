using System;
using System.Drawing;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers.TextureProviders
{
    public static class AgentTextureProvider
    {
        private static readonly IAgentTextureProvider[] AgentTextureProviders = new IAgentTextureProvider[]
        {
           new HumanAgentTextureProvider(),
           new HorseAgentTextureProvider(),
           new OtherAgentTextureProvider(),
           new DeadAgentTextureProvider()
        };

        public static Bitmap GetBitmap(this AgentMarkerTextureType agentMarkerType)
        {
            if (agentMarkerType < 0 || agentMarkerType >= AgentMarkerTextureType.Count)
                throw new ArgumentOutOfRangeException(nameof(agentMarkerType));

            return AgentTextureProviders[(int)agentMarkerType].GetBitmap();
        }

        public static Texture GetTexture(this AgentMarkerTextureType agentMarkerType)
        {
            if (agentMarkerType < 0 || agentMarkerType >= AgentMarkerTextureType.Count)
                throw new ArgumentOutOfRangeException(nameof(agentMarkerType));

            return AgentTextureProviders[(int)agentMarkerType].GetTexture();
        }
    }
}
