using System;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker.TextureProviders
{
   public static class AgentTextureProvider
   {
       private static readonly IAgentTextureProvider[] AgentTextureProviders = new IAgentTextureProvider[]
       {
           new MeleeAgentTextureProvider(),
           new RangedAgentTextureProvider(),
           new HorseAgentTextureProvider(),
           new OtherAgentTextureProvider(),
           new DeadAgentTextureProvider()
       };
        public static Texture GetTexture(this AgentMarkerType agentMarkerType)
        {
            if (agentMarkerType < 0 || agentMarkerType >= AgentMarkerType.Count)
                throw new ArgumentOutOfRangeException(nameof(agentMarkerType));

            return AgentTextureProviders[(int)agentMarkerType].GetTexture();
        }
    }
}
