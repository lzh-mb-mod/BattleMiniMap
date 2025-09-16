using TaleWorlds.Library;

namespace BattleMiniMap.View.AgentMarkers.Colors
{
    public enum AgentMarkerColorType
    {
        Inactive, PlayerTeam, PlayerTeamHighlight, PlayerTeamEscaping, PlayerAlly, PlayerAllyHighlight, PlayerAllyTeamEscaping, PlayerEnemy, PlayerEnemyHighlight, PlayerEnemyTeamEscaping, Human, HumanHighlight, Horse, Other, Count
    }

    public static class AgentMarkerColorTypeExtension
    {
        public static Color GetColor(this AgentMarkerColorType colorType)
        {
            switch (colorType)
            {
                case AgentMarkerColorType.Inactive:
                    return new Color(0.45f, 0.4f, 0.4f, 0.8f);
                case AgentMarkerColorType.PlayerTeam:
                    return new Color(0.19f, 0.45f, 0.70f);
                case AgentMarkerColorType.PlayerTeamHighlight:
                    return new Color(0.23f, 0.55f, 0.95f);
                case AgentMarkerColorType.PlayerTeamEscaping:
                    return new Color(0.50f, 0.60f, 0.70f);
                case AgentMarkerColorType.PlayerAlly:
                    return new Color(0.25f, 0.75f, 0.38f);
                case AgentMarkerColorType.PlayerAllyHighlight:
                    return new Color(0.25f, 0.95f, 0.38f);
                case AgentMarkerColorType.PlayerAllyTeamEscaping:
                    return new Color(0.54f, 0.68f, 0.57f);
                case AgentMarkerColorType.PlayerEnemy:
                    return new Color(0.68f, 0.14f, 0.24f);
                case AgentMarkerColorType.PlayerEnemyHighlight:
                    return new Color(0.95f, 0.2f, 0.4f);
                case AgentMarkerColorType.PlayerEnemyTeamEscaping:
                    return new Color(0.68f, 0.41f, 0.47f);
                case AgentMarkerColorType.Human:
                    return new Color(0.7f, 0.7f, 0.18f);
                case AgentMarkerColorType.HumanHighlight:
                    return new Color(0.7f, 0.7f, 0.18f);
                case AgentMarkerColorType.Horse:
                    return new Color(0.3f, 0.3f, 0.2f);
                case AgentMarkerColorType.Other:
                    return new Color(0.4f, 0.4f, 0.2f);
                default:
                    return new Color(0, 0, 0);
            }
        }
    }
}
