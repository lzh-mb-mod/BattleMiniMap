using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using BattleMiniMap.View.MapTerrain;
using System;
using System.Collections.Generic;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.TwoDimension;
using static TaleWorlds.MountAndBlade.Source.Objects.Siege.AgentPathNavMeshChecker;

namespace BattleMiniMap.View.AgentMarkers
{
    public class BattleMiniMap_AgentMarkerCollectionWidget : BrushWidget
    {
        private DrawObject2D _cachedMesh;
        private float _agentMarkerSize;
        private bool _isLastDrawHero;

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
                _agentMarkerSize = Math.Max(SuggestedWidth * (config.FollowMode ? config.GetFollowModeScale() * 0.025f : 0.005f) * config.AgentMarkerScale, 3);
            }
        }

        private float GetAgentMarkerSize(ColorAndTexturePair type)
        {
            var config = BattleMiniMapConfig.Get();
            var markerScale = type.TextureType == AgentMarkerTextureType.Hero ? 5f * HeroAgentTextureProvider.HeroTextureScale : config.AgentMarkerScale;
            return Math.Max(SuggestedWidth * (config.FollowMode ? config.GetFollowModeScale() * 0.025f : 0.005f) * markerScale, 3);
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);

            var uiScale = _scaleToUse;
            
            var materials = new Dictionary<ColorAndTexturePair, SimpleMaterial>();
            var globalPosition = Widgets.Utility.GetGlobalPosition(this);

            for (int i = 0; i < AgentMakers.CountOfAgentMarkers; ++i)
            {
                var agentMaker = AgentMakers.AgentMarkers[i];
                var type = agentMaker.AgentMarkerType;
                if (!materials.TryGetValue(type, out var material))
                {
                    material = materials[type] = CreateMaterial(drawContext, type);
                }
                var scaledMarkerSize = GetAgentMarkerSize(type) * uiScale;
                var x = globalPosition.x + agentMaker.PositionInWidget.x * uiScale - scaledMarkerSize * 0.5f;
                var y = globalPosition.y + agentMaker.PositionInWidget.y * uiScale - scaledMarkerSize * 0.5f;
                UpdateDrawObject2D(x, y, scaledMarkerSize, scaledMarkerSize, type.TextureType == AgentMarkerTextureType.Hero, agentMaker.Direction);
                twoDimensionContext.Draw(x, y, material, _cachedMesh, type.GetLayer());
            }
        }

        private SimpleMaterial CreateMaterial(TwoDimensionDrawContext drawContext, ColorAndTexturePair type)
        {
            var material = Widgets.Utility.CreateMaterial(drawContext, this);
            material.Texture = type.TextureType.GetTexture();
            material.Color *= type.ColorType.GetColor();
            return material;
        }

        private void UpdateDrawObject2D(float x, float y, float width, float height, bool isHero, Vec2 agentDirection)
        {
            if (_cachedMesh == null || Math.Abs(_cachedMesh.Width - width) > 0.01f && Math.Abs(_cachedMesh.Height - height) > 0.01f || isHero || _isLastDrawHero)
            {
                if (isHero)
                {
                    var config = BattleMiniMapConfig.Get();
                    float angle;
                    if (config.FollowMode)
                    {
                        var camera = MissionState.Current.GetListenerOfType<MissionScreen>().CombatCamera;
                        var cameraDirection = camera.Direction.AsVec2.Normalized();
                        angle = -agentDirection.AngleBetween(cameraDirection);
                    }
                    else
                    {
                        angle = -agentDirection.LeftVec().AngleBetween(-Vec2.Forward);
                    }

                    _cachedMesh = Widgets.Utility.CreateDrawObject2D(width, height, new Vec2(0, 0), new Vec2(width / 2, height / 2), angle);
                }
                else
                {
                    _cachedMesh = Widgets.Utility.CreateDrawObject2D(width, height);
                }
            }
        }
    }
}
