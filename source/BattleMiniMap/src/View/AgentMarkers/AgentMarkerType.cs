using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using System;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarkers
{

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

        public static ColorAndTexturePair GetColorAndTextureType(this Agent agent)
        {
            if (!agent.IsActive())
                return new ColorAndTexturePair(AgentMarkerColorType.Inactive, AgentMarkerTextureType.Dead);

            if (agent.IsHuman)
            {
                return GetHumanMarkerType(agent);
            }

            if (agent.RiderAgent != null)
            {
                var riderType = GetHumanMarkerType(agent.RiderAgent);
                return new ColorAndTexturePair(riderType.ColorType, AgentMarkerTextureType.Horse);
            }

            return agent.IsMount ?
                new ColorAndTexturePair(AgentMarkerColorType.Horse, AgentMarkerTextureType.Horse) :
                new ColorAndTexturePair(AgentMarkerColorType.Other, AgentMarkerTextureType.OtherAnimal);
        }

        public static int GetLayer(this ColorAndTexturePair type)
        {
            if (type.ColorType == AgentMarkerColorType.PlayerTeam || type.ColorType == AgentMarkerColorType.PlayerTeamHighlight)
            {
                return 7;
            }
            switch (type.TextureType)
            {
                case AgentMarkerTextureType.Hero:
                    return 6;
                case AgentMarkerTextureType.Human:
                    return 5;
                case AgentMarkerTextureType.Horse:
                    return 4;
                case AgentMarkerTextureType.OtherAnimal:
                    return 3;
                case AgentMarkerTextureType.Dead:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static ColorAndTexturePair GetHumanMarkerType(bool isEnemy, bool isPlayerTeam, bool isHero, bool isRunningAway)
        {
            AgentMarkerColorType colorType;
            AgentMarkerTextureType textureType;
            if (isEnemy)
            {
                if (isHero)
                {
                    colorType = isRunningAway ? AgentMarkerColorType.PlayerEnemyTeamEscaping : AgentMarkerColorType.PlayerEnemyHighlight;
                    textureType = AgentMarkerTextureType.Hero;
                }
                else
                {
                    colorType = isRunningAway ? AgentMarkerColorType.PlayerEnemyTeamEscaping : AgentMarkerColorType.PlayerEnemy;
                    textureType = AgentMarkerTextureType.Human;
                }
            }
            else if (isPlayerTeam)
            {
                if (isHero)
                {
                    colorType = isRunningAway ? AgentMarkerColorType.PlayerTeamEscaping : AgentMarkerColorType.PlayerTeamHighlight;
                    textureType = AgentMarkerTextureType.Hero;
                }
                else
                {
                    colorType = isRunningAway ? AgentMarkerColorType.PlayerTeamEscaping : AgentMarkerColorType.PlayerTeam;
                    textureType = AgentMarkerTextureType.Human;
                }
            }
            else
            {
                if (isHero)
                {
                    colorType = isRunningAway ? AgentMarkerColorType.PlayerAllyTeamEscaping : AgentMarkerColorType.PlayerAllyHighlight;
                    textureType = AgentMarkerTextureType.Hero;
                }
                else
                {
                    colorType = isRunningAway ? AgentMarkerColorType.PlayerAllyTeamEscaping : AgentMarkerColorType.PlayerAlly;
                    textureType = AgentMarkerTextureType.Human;
                }
            }
            return new ColorAndTexturePair(colorType, textureType);
        }

        private static ColorAndTexturePair GetHumanMarkerType(Agent agent)
        {
            bool isEnemy = false, isPlayerTeam = false, isHero = agent.IsHero, isRunningAway = agent.IsRunningAway;
            AgentMarkerColorType colorType;
            AgentMarkerTextureType textureType;
            if (agent.Team == null || !agent.Team.IsValid || Mission.Current.PlayerTeam == null)
            {
                if (Mission.Current.MainAgent != null)
                {
                    if (agent.IsEnemyOf(Mission.Current.MainAgent))
                    {
                        isEnemy = true;
                        isPlayerTeam = false;
                    }

                    if (agent.IsFriendOf(Mission.Current.MainAgent))
                    {
                        isEnemy = false;
                        isPlayerTeam = false;
                    }
                    return GetHumanMarkerType(isEnemy, isPlayerTeam, isHero, isRunningAway);
                }
                else
                {
                    if (isHero)
                    {
                        colorType = AgentMarkerColorType.HumanHighlight;
                        textureType = AgentMarkerTextureType.Hero;
                    }
                    else
                    {
                        colorType = AgentMarkerColorType.Human;
                        textureType = AgentMarkerTextureType.Human;
                    }
                    return new ColorAndTexturePair(colorType, textureType);
                }
            }

            if (agent.Team.IsPlayerTeam)
            {
                isEnemy = false;
                isPlayerTeam = true;
                return GetHumanMarkerType(isEnemy, isPlayerTeam, isHero, isRunningAway);
            }

            if (agent.Team.IsPlayerAlly)
            {
                isEnemy = false;
                isPlayerTeam = false;
                return GetHumanMarkerType(isEnemy, isPlayerTeam, isHero, isRunningAway);
            }

            if (agent.Team.IsEnemyOf(Mission.Current.PlayerTeam))
            {
                isEnemy = true;
                isPlayerTeam = false;
                return GetHumanMarkerType(isEnemy, isPlayerTeam, isHero, isRunningAway);
            }

            if (isHero)
            {
                colorType = AgentMarkerColorType.HumanHighlight;
                textureType = AgentMarkerTextureType.Hero;
            }
            else
            {
                colorType = AgentMarkerColorType.Human;
                textureType = AgentMarkerTextureType.Human;
            }
            return new ColorAndTexturePair(colorType, textureType);
        }
    }
}
