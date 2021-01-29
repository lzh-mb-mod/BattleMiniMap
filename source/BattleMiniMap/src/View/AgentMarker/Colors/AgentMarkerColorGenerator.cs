using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarker.Colors
{
    public class AgentMarkerColorGenerator
    {
        public static Color GetAgentMarkerColor(Agent agent)
        {
            if (!agent.IsActive())
            {
                return new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }
            if (agent.IsHuman)
            {
                return GetHumanColor(agent);
            }

            if (agent.RiderAgent != null)
            {
                return GetHumanColor(agent.RiderAgent);
            }

            return GetAnimalColor(agent);
        }

        private static Color GetHumanColor(Agent agent)
        {
            if (agent.Team == null)
            {
                if (Mission.Current.MainAgent != null)
                {
                    if (agent.IsEnemyOf(Mission.Current.MainAgent))
                    {
                        return new Color(0.98f, 0.42f, 0.24f);
                    }
                    else if (agent.IsFriendOf(Mission.Current.MainAgent))
                    {
                        return new Color(0.25f, 0.75f, 0.38f);
                    }
                }
                return new Color(0.6f, 0.6f, 0.6f);
            }

            if (agent.Team == Mission.Current.PlayerTeam)
            {
                return new Color(0.25f, 0.60f, 0.94f);
            }
            else if (agent.Team == Mission.Current.PlayerAllyTeam)
            {
                return new Color(0.25f, 0.75f, 0.38f);
            }
            else if (agent.Team == Mission.Current.PlayerEnemyTeam)
            {
                return new Color(0.91f, 0.18f, 0.32f);
            }
            return new Color(0.7f, 0.7f, 0.7f);
        }

        private static Color GetAnimalColor(Agent agent)
        {
            return new Color(0.5f, 0.5f, 0.5f);
        }
    }
}
