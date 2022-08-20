using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using System;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarkers
{
    public enum AgentMarkerType
    {
        PlayerTeamHuman,
        PlayerTeamEscapingHuman,
        PlayerTeamHorse,
        PlayerTeamEscapingHorse,
        PlayerAllyTeamHuman,
        PlayerAllyTeamEscapingHuman,
        PlayerAllyTeamHorse,
        PlayerAllyTeamEscapingHorse,
        PlayerEnemyTeamHuman,
        PlayerEnemyTeamEscapingHuman,
        PlayerEnemyTeamHorse,
        PlayerEnemyTeamEscapingHorse,
        Human,
        EscapingHuman,
        Horse,
        EscapingHorse,
        Animal,
        Inactive,
        Count
    }

    public struct ColorAndTexturePair
    {
        public AgentMarkerColorType ColorType;
        public AgentMarkerTextureType TextureType;

        public ColorAndTexturePair(AgentMarkerColorType colorType, AgentMarkerTextureType textureType)
        {
            ColorType = colorType;
            TextureType = textureType;
        }
    }

    public static class AgentMarkerTypeExtension
    {
        private static int _errorCount;

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
                return GetHumanMarkerType(agent.RiderAgent) + 2;
            }

            return agent.IsMount ? AgentMarkerType.Horse : AgentMarkerType.Animal;
        }

        public static ColorAndTexturePair GetColorAndTextureType(
            this AgentMarkerType type)
        {
            switch (type)
            {
                case AgentMarkerType.Inactive:
                    return new ColorAndTexturePair(AgentMarkerColorType.Inactive,
                        AgentMarkerTextureType.Dead);
                case AgentMarkerType.PlayerTeamHuman:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerTeam,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerTeamEscapingHuman:
                    return new ColorAndTexturePair(
                        AgentMarkerColorType.PlayerTeamEscaping, AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerTeamHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerTeam,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerTeamEscapingHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerTeamEscaping,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerAllyTeamHuman:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerAlly,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerAllyTeamEscapingHuman:
                    return new ColorAndTexturePair(
                        AgentMarkerColorType.PlayerAllyTeamEscaping, AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerAllyTeamHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerAlly,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerAllyTeamEscapingHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerAllyTeamEscaping,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerEnemyTeamHuman:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerEnemy,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerEnemyTeamEscapingHuman:
                    return new ColorAndTexturePair(
                        AgentMarkerColorType.PlayerEnemyTeamEscaping, AgentMarkerTextureType.Human);
                case AgentMarkerType.PlayerEnemyTeamHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerEnemy,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.PlayerEnemyTeamEscapingHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.PlayerEnemyTeamEscaping,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.Human:
                    return new ColorAndTexturePair(AgentMarkerColorType.Human,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.EscapingHuman:
                    return new ColorAndTexturePair(AgentMarkerColorType.Human,
                        AgentMarkerTextureType.Human);
                case AgentMarkerType.Horse:
                    return new ColorAndTexturePair(AgentMarkerColorType.Horse,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.EscapingHorse:
                    return new ColorAndTexturePair(AgentMarkerColorType.Horse,
                        AgentMarkerTextureType.Horse);
                case AgentMarkerType.Animal:
                    return new ColorAndTexturePair(AgentMarkerColorType.Other,
                        AgentMarkerTextureType.OtherAnimal);
                default:
                {
                    if (_errorCount < 10)
                    {
                        ++_errorCount;
                        MissionSharedLibrary.Utilities.Utility.DisplayMessageForced(
                            $"Error: Unexpected agent type '{type}'.");
                    }
                    return new ColorAndTexturePair(AgentMarkerColorType.Other,
                        AgentMarkerTextureType.OtherAnimal);
                }
            }
        }

        public static int GetLayer(this AgentMarkerType type)
        {
            switch (type)
            {
                case AgentMarkerType.PlayerTeamHuman:
                case AgentMarkerType.PlayerTeamEscapingHuman:
                case AgentMarkerType.PlayerAllyTeamHuman:
                case AgentMarkerType.PlayerAllyTeamEscapingHuman:
                case AgentMarkerType.PlayerEnemyTeamHuman:
                case AgentMarkerType.PlayerEnemyTeamEscapingHuman:
                case AgentMarkerType.Human:
                case AgentMarkerType.EscapingHuman:
                    return 5;
                case AgentMarkerType.PlayerTeamHorse:
                case AgentMarkerType.PlayerTeamEscapingHorse:
                case AgentMarkerType.PlayerAllyTeamHorse:
                case AgentMarkerType.PlayerAllyTeamEscapingHorse:
                case AgentMarkerType.PlayerEnemyTeamHorse:
                case AgentMarkerType.PlayerEnemyTeamEscapingHorse:
                case AgentMarkerType.Horse:
                case AgentMarkerType.EscapingHorse:
                    return 4;
                case AgentMarkerType.Animal:
                    return 3;
                case AgentMarkerType.Inactive:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static AgentMarkerType GetHumanMarkerType(Agent agent)
        {
            if (agent.Team == null || !agent.Team.IsValid)
            {
                if (Mission.Current.MainAgent != null)
                {
                    if (agent.IsEnemyOf(Mission.Current.MainAgent))
                    {
                        return agent.IsRunningAway ? AgentMarkerType.PlayerEnemyTeamEscapingHuman : AgentMarkerType.PlayerEnemyTeamHuman;
                    }

                    if (agent.IsFriendOf(Mission.Current.MainAgent))
                    {
                        return agent.IsRunningAway ? AgentMarkerType.PlayerAllyTeamEscapingHuman : AgentMarkerType.PlayerAllyTeamHuman;
                    }
                }

                return AgentMarkerType.Human;
            }

            if (agent.Team == Mission.Current.PlayerTeam)
            {
                return agent.IsRunningAway ? AgentMarkerType.PlayerTeamEscapingHuman : AgentMarkerType.PlayerTeamHuman;
            }

            if (agent.Team.IsPlayerAlly)
            {
                return agent.IsRunningAway ? AgentMarkerType.PlayerAllyTeamEscapingHuman : AgentMarkerType.PlayerAllyTeamHuman;
            }

            if (agent.Team == Mission.Current.PlayerEnemyTeam)
            {
                return agent.IsRunningAway ? AgentMarkerType.PlayerEnemyTeamEscapingHuman : AgentMarkerType.PlayerEnemyTeamHuman;
            }

            return AgentMarkerType.Human;
        }
    }
}
