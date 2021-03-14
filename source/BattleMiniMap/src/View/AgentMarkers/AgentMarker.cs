using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarkers
{
    public class AgentMarker
    {
        private Agent _agent;

        public Vec2 PositionInMap { get; set; }

        public Vec2 PositionInWidget { get; set; }

        public AgentMarkerType AgentMarkerType { get; set; }

        public AgentMarker(Agent agent)
        {
            _agent = agent;
            Update();
        }

        public AgentMarker(AgentMarker other)
        {
            CopyFrom(other);
        }

        public void Update()
        {
            if (AgentMarkerType == AgentMarkerType.Inactive)
                return;

            UpdateMarker();
        }

        public void CopyFrom(AgentMarker other)
        {
            PositionInMap = other.PositionInMap;
            PositionInWidget = other.PositionInWidget;
            AgentMarkerType = other.AgentMarkerType;
            _agent = other._agent;
        }

        public void SetAgent(Agent agent)
        {
            _agent = agent;
            UpdateMarker();
        }

        public void Clear()
        {
            _agent = null;
        }

        private void UpdateMarker()
        {
            AgentMarkerType = _agent.GetAgentMarkerType();
            if (AgentMarkerType == AgentMarkerType.Inactive)
            {
                MakeDead();
                return;
            }

            var miniMap = MiniMap.Instance;
            if (!miniMap.IsValid && !BattleMiniMapConfig.Get().ShowMap)
                return;
            PositionInMap = miniMap.WorldToMapF(_agent.Position.AsVec2);
            PositionInWidget = miniMap.MapToWidget(PositionInMap);
        }

        private void MakeDead()
        {
            PositionInMap = MiniMap.Instance.WorldToMapF(_agent.Position.AsVec2);
            PositionInWidget = MiniMap.Instance.MapToWidget(PositionInMap);
            _agent = null;
        }
    }
}
