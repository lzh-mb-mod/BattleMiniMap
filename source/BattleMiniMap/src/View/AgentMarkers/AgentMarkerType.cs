using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using System;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarkers
{
    public enum AgentMarkerType
    {
        PlayerTeamHuman, PlayerTeamHorse, PlayerAllyTeamHuman, PlayerAllyTeamHorse, PlayerEnemyTeamHuman, PlayerEnemyTeamHorse, Human, Horse, Animal, Inactive, Count
    }

    public static class AgentMarkerTypeExtension
    {
        public static AgentMarkerType GetAgentMarkerType(this Agent agent)
        {
            if (!agent.IsActive())
                return AgentMarkerType.Inactive;

            if (agent.IsHuman)
            {
                return GetHumanMarkerType(agent);
            }

            if (agent.RiderAgent != null)
            {
                return GetHumanMarkerType(agent.RiderAgent) + 1;
            }

            return agent.IsMount ? AgentMarkerType.Horse : AgentMarkerType.Animal;
        }

        public static Tuple<AgentMarkerColorType, AgentMarkerTextureType> GetColorAndTextureType(
            this AgentMarkerType type)
        {
            switch (type)
            {
                case AgentMarkerType.Inactive:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.Inactive,
                        AgentMarkerTextureType.Dead);
                case AgentMarkerType.PlayerTeamHuman:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.PlayerTeam,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerTeamHorse:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.PlayerTeam,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerAllyTeamHuman:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.PlayerAlly,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerAllyTeamHorse:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.PlayerAlly,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerEnemyTeamHuman:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.PlayerEnemy,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerEnemyTeamHorse:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.PlayerEnemy,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.Human:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.Human,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.Horse:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.Horse,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.Animal:
                    return new Tuple<AgentMarkerColorType, AgentMarkerTextureType>(AgentMarkerColorType.Other,
                        AgentMarkerTextureType.OtherAnimal);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static int GetLayer(this AgentMarkerType type)
        {
            switch (type)
            {
                case AgentMarkerType.Inactive:
                    return 2;
                case AgentMarkerType.PlayerTeamHuman:
                case AgentMarkerType.PlayerAllyTeamHuman:
                case AgentMarkerType.PlayerEnemyTeamHuman:
                case AgentMarkerType.Human:
                    return 5;
                case AgentMarkerType.PlayerTeamHorse:
                case AgentMarkerType.PlayerAllyTeamHorse:
                case AgentMarkerType.PlayerEnemyTeamHorse:
                case AgentMarkerType.Horse:
                    return 4;
                case AgentMarkerType.Animal:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static AgentMarkerType GetHumanMarkerType(Agent agent)
        {
            if (agent.Team == null)
            {
                if (Mission.Current.MainAgent != null)
                {
                    if (agent.IsEnemyOf(Mission.Current.MainAgent))
                    {
                        return AgentMarkerType.PlayerAllyTeamHuman;
                    }
                    else if (agent.IsFriendOf(Mission.Current.MainAgent))
                    {
                        return AgentMarkerType.PlayerAllyTeamHuman;
                    }
                }

                return AgentMarkerType.Human;
            }

            if (agent.Team == Mission.Current.PlayerTeam)
            {
                return AgentMarkerType.PlayerTeamHuman;
            }

            if (agent.Team.IsPlayerAlly)
            {
                return AgentMarkerType.PlayerAllyTeamHuman;
            }

            if (agent.Team == Mission.Current.PlayerEnemyTeam)
            {
                return AgentMarkerType.PlayerEnemyTeamHuman;
            }

            return AgentMarkerType.Human;
        }
    }
}
