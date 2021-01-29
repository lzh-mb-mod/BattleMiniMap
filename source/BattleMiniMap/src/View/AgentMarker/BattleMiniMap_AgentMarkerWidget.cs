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
        private Vec2 _basePosition1;

        public override Texture Texture => AgentMarkerType.GetTexture();

        public AgentMarkerType AgentMarkerType { get; set; }

        public Vec2 Center { get; set; }
        public Vec2 ActualCenter => new Vec2(Center.x * SuggestedWidth, Center.y * SuggestedHeight);

        public Vec2 BasePosition
        {
            get => _basePosition1;
            set
            {
                _basePosition1 = value;
                PositionXOffset = _basePosition1.x - ActualCenter.x;
                PositionYOffset = _basePosition1.y - ActualCenter.y;
            }
        }

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

                SuggestedWidth = Math.Max(width * 0.02f, 1);
                SuggestedHeight = Math.Max(width * 0.02f, 1);
                IsEnabled = MiniMap.Instance.IsEnabled;
            }
        }
    }
}
