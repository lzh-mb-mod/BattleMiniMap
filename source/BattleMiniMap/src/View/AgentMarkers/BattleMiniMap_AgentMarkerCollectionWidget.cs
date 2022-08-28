using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using BattleMiniMap.View.MapTerrain;
using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers
{
    public class BattleMiniMap_AgentMarkerCollectionWidget : BrushWidget
    {
        private DrawObject2D _cachedMesh;
        private float _agentMarkerSize;

        public BattleMiniMap_AgentMarkerCollectionWidget(UIContext context) : base(context)
        {
            WidthSizePolicy = HeightSizePolicy = SizePolicy.Fixed;
        }

        public AgentMarkerCollection AgentMakers { get; set; }


        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);

            if (MiniMap.Instance != null)
            {
                var width = MiniMap.Instance.BitmapWidth;
                var height = MiniMap.Instance.BitmapHeight;
                var config = BattleMiniMapConfig.Get();
                SuggestedWidth = config.WidgetWidth;
                SuggestedHeight = height / (float)width * SuggestedWidth;
                //var size = Utility.WorldToMapF(10, MiniMap.Instance.Resolution, 0);
                //_agentMarkerSize = Math.Max(MiniMap.Instance.MapFToWidget(size) * config.AgentMarkerScale, 1);
                var size = Widgets.Utility.GetSize(this);
                _agentMarkerSize = Math.Max(size.x * (config.FollowMode ? config.GetFollowModeScale() * 0.025f : 0.01f) * config.AgentMarkerScale, 3);
            }
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);

            var size = _agentMarkerSize;
            UpdateDrawObject2D(size, size);
            var materials = new SimpleMaterial[(int)AgentMarkerType.Count];
            var globalPosition = Widgets.Utility.GetGlobalPosition(this);

            for (int i = 0; i < AgentMakers.CountOfAgentMarkers; ++i)
            {
                var agentMaker = AgentMakers.AgentMarkers[i];
                var type = agentMaker.AgentMarkerType;
                twoDimensionContext.Draw(globalPosition.x + agentMaker.PositionInWidget.x * ScaledSuggestedWidth / Math.Max(SuggestedWidth, 1) - size * 0.5f, globalPosition.y + agentMaker.PositionInWidget.y * ScaledSuggestedHeight / Math.Max(SuggestedHeight, 1) - size * 0.5f, materials[(int)type] ??= CreateMaterial(drawContext, type), _cachedMesh, type.GetLayer());
            }
        }

        private SimpleMaterial CreateMaterial(TwoDimensionDrawContext drawContext, AgentMarkerType type)
        {
            var types = type.GetColorAndTextureType();
            var material = Widgets.Utility.CreateMaterial(drawContext, this);
            material.Texture = types.TextureType.GetTexture();
            material.Color *= types.ColorType.GetColor();
            return material;
        }

        private void UpdateDrawObject2D(float width, float height)
        {
            if (_cachedMesh == null || Math.Abs(_cachedMesh.Width - width) > 0.01f && Math.Abs(_cachedMesh.Height - height) > 0.01f)
            {
                _cachedMesh = Widgets.Utility.CreateDrawObject2D(width, height);
            }
        }
    }
}
