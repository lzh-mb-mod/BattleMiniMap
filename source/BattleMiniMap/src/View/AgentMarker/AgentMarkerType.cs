using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarker
{
    public enum AgentMarkerType
    {
        Melee, Ranged, Horse, Other, Dead, Count
    }

    public static class AgentMarkerTypeGenerator
    {
        public static AgentMarkerType GetAgentMarkerType(this Agent agent)
        {
            if (!agent.IsActive())
                return AgentMarkerType.Dead;

            if (agent.IsHuman)
            {
                // Temporarily use Circle only, because rotate the marker may cause bad performance.
                return AgentMarkerType.Ranged;
                //    if (QueryLibrary.IsInfantry(agent) || QueryLibrary.IsCavalry(agent))
                //        return AgentMarkerType.Melee;
                //    if (QueryLibrary.IsRanged(agent) || QueryLibrary.IsRangedCavalry(agent))
                //        return AgentMarkerType.Ranged;
            }

            return agent.IsMount ? AgentMarkerType.Horse : AgentMarkerType.Other;
        }
    }
}
