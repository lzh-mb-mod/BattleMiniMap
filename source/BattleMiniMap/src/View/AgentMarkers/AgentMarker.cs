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
        public Vec2 Direction { get; set; }

        public ColorAndTexturePair AgentMarkerType { get; set; } = new ColorAndTexturePair(Colors.AgentMarkerColorType.Other, TextureProviders.AgentMarkerTextureType.OtherAnimal);

        public AgentMarker(Agent agent)
        {
            _agent = agent;
            InitializeMarker();
        }

        public AgentMarker(AgentMarker other)
        {
            CopyFrom(other);
        }

        private void InitializeMarker()
        {
            AgentMarkerType = _agent.GetColorAndTextureType();
            if (AgentMarkerType.TextureType == TextureProviders.AgentMarkerTextureType.Hero)
            {
                UpdateAsHero();
            }
            else
            {
                UpdateAsNonHero();
            }
        }

        public void UpdateAsNonHero()
        {
            if (AgentMarkerType.ColorType == Colors.AgentMarkerColorType.Inactive)
                return;
            UpdateMarkerPosition();
        }

        public void UpdateAsHero()
        {
            if (AgentMarkerType.ColorType == Colors.AgentMarkerColorType.Inactive)
                return;

            UpdateMarkerPositionAndDirection();
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
            UpdateMarkerPosition();
        }

        public void Clear()
        {
            _agent = null;
        }

        private void UpdateMarkerPosition()
        {
            AgentMarkerType = _agent.GetColorAndTextureType();
            if (AgentMarkerType.ColorType == Colors.AgentMarkerColorType.Inactive)
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

        private void UpdateMarkerPositionAndDirection()
        {
            AgentMarkerType = _agent.GetColorAndTextureType();
            if (AgentMarkerType.ColorType == Colors.AgentMarkerColorType.Inactive)
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
            Direction = _agent.GetMovementDirection();
            _agent = null;
        }
    }
}
