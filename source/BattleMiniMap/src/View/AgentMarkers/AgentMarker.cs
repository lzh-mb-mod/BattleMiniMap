using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.AgentMarkers
{
    public class AgentMarker
    {
        private Agent _agent;
        private Vec3 _cachedAgentPosition;

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

        public void RenderUpdate()
        {
            if (MiniMap.Instance?.IsValid ?? false)
            {
                UpdateRenderedPosition();
            }
        }

        public void CopyFrom(AgentMarker other)
        {
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

            _cachedAgentPosition = _agent.Position;
            var miniMap = MiniMap.Instance;
            if (!miniMap.IsValid && !BattleMiniMapConfig.Get().ShowMap)
                return;
            UpdateRenderedPosition();
        }

        private void UpdateRenderedPosition()
        {
            PositionInWidget = MiniMap.Instance.WorldToWidget(_cachedAgentPosition.AsVec2);
        }

        private void MakeDead()
        {
            PositionInWidget = MiniMap.Instance.WorldToWidget(_agent.Position.AsVec2);
            _agent = null;
        }
    }
}
