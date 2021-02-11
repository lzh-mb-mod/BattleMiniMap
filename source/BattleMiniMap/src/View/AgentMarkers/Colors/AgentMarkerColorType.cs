using TaleWorlds.Library;

namespace BattleMiniMap.View.AgentMarkers.Colors
{
    public enum AgentMarkerColorType
    {
        Inactive, PlayerTeam, PlayerAlly, PlayerEnemy, Human, Horse, Other, Count
    }

    public static class AgentMarkerColorTypeExtension
    {
        public static Color GetColor(this AgentMarkerColorType colorType)
        {
            switch (colorType)
            {
                case AgentMarkerColorType.Inactive:
                    return new Color(0.5f, 0.4f, 0.4f, 0.8f);
                case AgentMarkerColorType.PlayerTeam:
                    return new Color(0.25f, 0.60f, 0.94f);
                case AgentMarkerColorType.PlayerAlly:
                    return new Color(0.25f, 0.75f, 0.38f);
                case AgentMarkerColorType.PlayerEnemy:
                    return new Color(0.91f, 0.18f, 0.32f);
                case AgentMarkerColorType.Human:
                    return new Color(0.8f, 0.8f, 0.2f);
                case AgentMarkerColorType.Horse:
                    return new Color(0.3f, 0.3f, 0.2f);
                default:
                    return new Color(0.4f, 0.4f, 0.2f);
            }
        }
    }
}
