using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarker.TextureProviders;
using BattleMiniMap.View.MapTerrain;
using BattleMiniMap.Widgets;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker
{
    public class BattleMiniMap_AgentMarkerWidget : MapItemWidget
    {
        private AgentMarkerType _agentMarkerType;
        public override Texture Texture => AgentMarkerType.GetTexture();

        public AgentMarkerType AgentMarkerType
        {
            get => _agentMarkerType;
            set
            {
                _agentMarkerType = value;
                switch (value)
                {
                    case AgentMarkerType.Dead:
                        Layer = 2;
                        break;
                    case AgentMarkerType.Ranged:
                    case AgentMarkerType.Melee:
                        Layer = 5;
                        break;
                    case AgentMarkerType.Horse:
                        Layer = 4;
                        break;
                    case AgentMarkerType.Other:
                        Layer = 3;
                        break;
                }
            }
        }

        public Vec2 Center { get; set; }

        public Vec2 BasePosition { get; set; }

        public BattleMiniMap_AgentMarkerWidget(UIContext context) : base(context)
        {
            Center = new Vec2(0.5f, 0.5f);
            Update();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);

            Update();
        }

        private void Update()
        {
            if (MiniMap.Instance != null)
            {
                var width = BattleMiniMapConfig.Get().WidgetWidth;

                SuggestedWidth = Math.Max(width * 0.01f, 1);
                SuggestedHeight = Math.Max(width * 0.01f, 1);
                PositionXOffset = BasePosition.x - Center.x * SuggestedWidth;
                PositionYOffset = BasePosition.y - Center.y * SuggestedHeight;
                IsEnabled = MiniMap.Instance.IsValid;
            }
        }
    }
}
