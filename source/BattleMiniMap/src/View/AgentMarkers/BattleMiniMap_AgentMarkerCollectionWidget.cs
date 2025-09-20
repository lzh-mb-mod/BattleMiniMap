using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using BattleMiniMap.View.MapTerrain;
using System;
using System.Collections.Generic;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarkers
{
    public class BattleMiniMap_AgentMarkerCollectionWidget : BrushWidget
    {

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
            }
        }

        private float GetAgentMarkerSize(ColorAndTexturePair type)
        {
            var config = BattleMiniMapConfig.Get();
            var markerScale = type.TextureType == AgentMarkerTextureType.Hero ? config.HeroMarkerScale * HeroAgentTextureProvider.HeroTextureScale : config.AgentMarkerScale;
            return Math.Max(SuggestedWidth * (config.FollowMode ? config.GetFollowModeScale() * 0.025f : 0.005f) * markerScale, 3);
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);

            var uiScale = _scaleToUse;
            
            var materials = new Dictionary<ColorAndTexturePair, SimpleMaterial>();

            for (int i = 0; i < AgentMakers.CountOfAgentMarkers; ++i)
            {
                var agentMaker = AgentMakers.AgentMarkers[i];
                var type = agentMaker.AgentMarkerType;
                if (!materials.TryGetValue(type, out var material))
                {
                    material = materials[type] = CreateMaterial(drawContext, type);
                }
                var scaledMarkerSize = GetAgentMarkerSize(type) * uiScale;
                var x = agentMaker.PositionInWidget.x * uiScale - scaledMarkerSize * 0.5f;
                var y = agentMaker.PositionInWidget.y * uiScale - scaledMarkerSize * 0.5f;
                var mesh = GetDrawObject2D(x, y, scaledMarkerSize, scaledMarkerSize, type.TextureType == AgentMarkerTextureType.Hero, agentMaker.Direction);
                mesh.Rectangle.CalculateVisualMatrixFrame();
                twoDimensionContext.DrawImage(material, mesh, type.GetLayer());
            }
        }

        private SimpleMaterial CreateMaterial(TwoDimensionDrawContext drawContext, ColorAndTexturePair type)
        {
            var material = Widgets.Utility.CreateMaterial2(drawContext, this);
            material.Texture = type.TextureType.GetTexture();
            material.Color *= type.ColorType.GetColor();
            return material;
        }

        private ImageDrawObject GetDrawObject2D(float x, float y, float width, float height, bool isHero, Vec2 agentDirection)
        {
            if (isHero)
            {
                var config = BattleMiniMapConfig.Get();
                float angle;
                if (config.FollowMode)
                {
                    var camera = MissionState.Current.GetListenerOfType<MissionScreen>().CombatCamera;
                    var cameraDirection = camera.Direction.AsVec2.Normalized();
                    angle = agentDirection.AngleBetween(cameraDirection);
                }
                else
                {
                    angle = agentDirection.LeftVec().AngleBetween(-Vec2.Forward);
                }

                return Widgets.Utility.CreateDrawObject2D(this, new Vec2(x, y), width, height, new Vec2(0.5f, 0.5f), angle);
            }
            else
            {
                return Widgets.Utility.CreateDrawObject2D(this, x, y, width, height);
            }
        }
    }
}
