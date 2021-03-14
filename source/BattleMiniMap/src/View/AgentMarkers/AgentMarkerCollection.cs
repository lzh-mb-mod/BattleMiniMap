using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarkers
{
    public class AgentMarkerCollection
    {
        public List<AgentMarker> AgentMarkers;
        public int CountOfAgentMarkers;

        public void Add(Agent agent)
        {
            if (CountOfAgentMarkers == AgentMarkers.Count)
                AgentMarkers.Add(new AgentMarker(agent));
            else
                AgentMarkers[CountOfAgentMarkers].SetAgent(agent);
            ++CountOfAgentMarkers;
        }

        public void Add(AgentMarker agentMarker)
        {
            if (CountOfAgentMarkers == AgentMarkers.Count)
                AgentMarkers.Add(new AgentMarker(agentMarker));
            else
                AgentMarkers[CountOfAgentMarkers].CopyFrom(agentMarker);
            ++CountOfAgentMarkers;
        }
    }
}
